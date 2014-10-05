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
using Framework.Attributes;
using Framework.Constants.Account;
using Framework.Constants.Misc;
using Framework.Cryptography.BNet;
using Framework.Database;
using Framework.Database.Auth.Entities;
using Framework.Logging;
using Framework.Misc;

namespace AuthServer.Commands
{
    class AccountCommands
    {
        [ConsoleCommand("CreateAccount", "")]
        public static void CreateAccount(string[] args)
        {
            var email = Command.Read<string>(args, 0);
            var password = Command.Read<string>(args, 1);

            if (email != null && password != null)
            {
                var salt = new byte[0].GenerateRandomKey(0x20).ToHexString();
                var result = DB.Auth.Any<Accounts>(a => a.Email == email);

                if (!result)
                {
                    var srp = new SRP6a(salt);

                    srp.CalculateX(email, password.ToUpper(), false);

                    var account = new Accounts
                    {
                        Email            = email,
                        PasswordVerifier = srp.V.ToHexString(),
                        Salt             = salt,
                        Region           = Regions.XX,
                    };

                    if (DB.Auth.Add(account))
                    {
                        var newAcc = DB.Auth.Single<Accounts>(r => r.Email == email);

                        DB.Auth.Execute("INSERT INTO account (id, username, email, expansion) VALUES (?, ?, ?, ?)", newAcc.Id, newAcc.Email, newAcc.Email, 5);

                        Log.Message(LogType.Normal, "Account {0} successfully created", email);
                    }
                }
                else
                    Log.Message(LogType.Error, "Account {0} already in database", email);
            }
        }

        [ConsoleCommand("CreateGameAccount", "")]
        public static void CreateGameAccount(string[] args)
        {
            var email = Command.Read<string>(args, 0);
            var game = Command.Read<string>(args, 1);
            var index = Command.Read<byte>(args, 2);

            if (email != "" && game != "" && index != 0)
            {
                var account = DB.Auth.Single<Accounts>(a => a.Email == email);

                if (account != null)
                {
                    var exists = account.GameAccounts != null ? account.GameAccounts.Any(ga => ga.Game == game && ga.Index == index) : false;

                    if (!exists)
                    {
                        var gameAccount = new GameAccounts
                        {
                            AccountId = account.Id,
                            Game      = game,
                            Index     = index,
                            Region    = Regions.XX,
                            Flags     = GameAccountFlags.None,
                            BoxLevel  = 5
                        };

                        if (DB.Auth.Add(gameAccount))
                            Log.Message(LogType.Normal, "GameAccount '{0}{1}' for Account '{2}' successfully created.", game, index, email);
                    }
                    else
                        Log.Message(LogType.Error, "GameAccount '{0}{1}' for Account '{2}' already in database", game, index, email);
                }
                else
                    Log.Message(LogType.Error, "Account '{0}' doesn't exist.", email);
            }
        }
    }
}
