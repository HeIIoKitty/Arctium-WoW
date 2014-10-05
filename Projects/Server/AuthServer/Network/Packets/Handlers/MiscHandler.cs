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

using System.Linq;
using AuthServer.Attributes;
using AuthServer.Constants.Authentication;
using AuthServer.Constants.Net;
using AuthServer.Managers;
using AuthServer.Network.Sessions;
using Framework.Constants.Misc;
using Framework.Database;
using Framework.Database.Auth.Entities;
using Framework.Logging;
using Framework.Network.Packets;

namespace AuthServer.Network.Packets.Handlers
{
    class MiscHandler
    {
        [AuthMessage(AuthClientMessage.InformationRequest, AuthChannel.BattleNet)]
        public static void OnInformationRequest(AuthPacket packet, Client client)
        {
            var session = client.Session;
            if (!Manager.GetState())
            {
                AuthHandler.SendAuthComplete(true, AuthResult.ServerBusy, client);
                return;
            }

            var game = packet.ReadFourCC();
            var os = packet.ReadFourCC();
            var language = packet.ReadFourCC();

            Log.Message(LogType.Debug, "Program: {0}", game);
            Log.Message(LogType.Debug, "Platform: {0}", os);
            Log.Message(LogType.Debug, "Locale: {0}", language);

            var componentCount = packet.Read<int>(6);

            for (int i = 0; i < componentCount; i++)
            {
                var program = packet.ReadFourCC();
                var platform = packet.ReadFourCC();
                var build = packet.Read<int>(32);

                Log.Message(LogType.Debug, "Program: {0}", program);
                Log.Message(LogType.Debug, "Platform: {0}", platform);
                Log.Message(LogType.Debug, "Locale: {0}", build);

                if (DB.Auth.Any<Components>(c => c.Program == program && c.Platform == platform && c.Build == build))
                    continue;

                if (!DB.Auth.Any<Components>(c => c.Program == program))
                {
                    AuthHandler.SendAuthComplete(true, AuthResult.InvalidProgram, client);
                    return;
                }

                if (!DB.Auth.Any<Components>(c => c.Platform == platform))
                {
                    AuthHandler.SendAuthComplete(true, AuthResult.InvalidPlatform, client);
                    return;
                }

                if (!DB.Auth.Any<Components>(c => c.Build == build))
                {
                    AuthHandler.SendAuthComplete(true, AuthResult.InvalidGameVersion, client);
                    return;
                }
            }

            var hasAccountName = packet.Read<bool>(1);

            if (hasAccountName)
            {
                var accountLength = packet.Read<int>(9) + 3;
                var accountName = packet.ReadString(accountLength);
                var account = DB.Auth.Single<Accounts>(a => a.Email == accountName);

                // First account lookup on database
                if ((session.Account = account) != null)
                {
                    // Global base account.
                    session.Account.IP = session.GetClientInfo();
                    session.Account.Language = language;

                    // Assign the possible game accounts based on the game.
                    //session.GameAccounts.ForEach(ga => ga.OS = os);

                    // Save the last changes
                    //DB.Auth.Update();

                    // Used for module identification.
                    client.Game = game;
                    client.OS = os;

                    AuthHandler.SendProofRequest(client);
                }
                else
                    AuthHandler.SendAuthComplete(true, AuthResult.BadLoginInformation, client);
            }
        }

        [AuthMessage(AuthClientMessage.Ping, AuthChannel.Creep)]
        public static void OnPing(AuthPacket packet, Client client)
        {
            var pong = new AuthPacket(AuthServerMessage.Pong, AuthChannel.Creep);

            client.SendPacket(pong);
        }

        [AuthMessage(AuthClientMessage.Disconnect, AuthChannel.Creep)]
        public static void OnDisconnect(AuthPacket packet, Client client)
        {
            Log.Message(LogType.Debug, "Client '{0}' disconnected.", client.ConnectionInfo);

            Manager.SessionMgr.RemoveClient(client.Id);
        }
    }
}
