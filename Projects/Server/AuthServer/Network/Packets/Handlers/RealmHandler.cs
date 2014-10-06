/*
 * Copyright (C) 2012-2014 Arctium Emulation <http://arctium.org>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Net;
using AuthServer.Attributes;
using AuthServer.Constants.Net;
using AuthServer.Managers;
using AuthServer.Network.Sessions;
using Framework.Constants.Misc;
using Framework.Logging;
using Framework.Misc;
using Framework.Network.Packets;

namespace AuthServer.Network.Packets.Handlers
{
    class RealmHandler
    {
        [AuthMessage(AuthClientMessage.JoinRequest, AuthChannel.WoW)]
        public static void OnJoinRequest(AuthPacket packet, Client client)
        {
            var clientSalt = BitConverter.GetBytes(packet.Read<uint>(32));
            var serverSalt = new byte[0].GenerateRandomKey(4);

            client.Session.GenerateSessionKey(clientSalt, serverSalt);

            // Continue if sessionKey is not empty
            if (client.Session.GameAccount.SessionKey != "")
            {
                var joinResponse = new AuthPacket(AuthServerMessage.JoinResponse, AuthChannel.WoW);

                joinResponse.Write(Manager.RealmMgr.RealmList.Count == 0, 1);

                joinResponse.Write(BitConverter.ToUInt32(serverSalt, 0), 32);
                joinResponse.Write(Manager.RealmMgr.RealmList.Count, 5);

                foreach (var realm in Manager.RealmMgr.RealmList)
                {
                    var ip = IPAddress.Parse(realm.Value.Address).GetAddressBytes();
                    var port = BitConverter.GetBytes((ushort)(realm.Value.Port));

                    Array.Reverse(port);

                    joinResponse.Write(ip);
                    joinResponse.Write(port);
                }

                joinResponse.Write(0, 5);

                client.SendPacket(joinResponse);
            }
        }

        [AuthMessage(AuthClientMessage.RealmUpdate, AuthChannel.WoW)]
        public static void OnRealmUpdate(AuthPacket packet, Client client)
        {
            Log.Message(LogType.Debug, "Received realm update.");

            var complete = new AuthPacket(AuthServerMessage.RealmComplete, AuthChannel.WoW);

            complete.Flush();
            complete.Write(0, 8);

            var realmCounter = 0;

            foreach (var realm in Manager.RealmMgr.RealmList)
            {
                var realmlist = new AuthPacket(AuthServerMessage.RealmUpdate, AuthChannel.WoW);

                realmlist.Write(true, 1);
                realmlist.Write(1, 32);
                realmlist.Write(0f, 32);
                realmlist.Write(realm.Value.RealmFlags, 8);
                realmlist.Write(realm.Value.Id, 19);
                realmlist.Write(0x80000000 + realm.Value.Icon, 32);
                realmlist.WriteString(realm.Value.Name, 10, false);
                realmlist.Write(false, 1);
                realmlist.Write(0, 8);
                realmlist.Write(0, 12);
                realmlist.Write(0, 8);
                realmlist.Write(0, 32);
                realmlist.Write(++realmCounter, 8);

                // End
                realmlist.Write(new byte[] { 0x43, 0x02 });
                realmlist.Finish();

                complete.Write(realmlist.Data);
            }

            client.SendPacket(complete);
        }
    }
}
