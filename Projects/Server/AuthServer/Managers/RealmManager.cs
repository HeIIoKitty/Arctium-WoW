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

using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using AuthServer.Configuration;
using Framework.Constants.Misc;
using Framework.Database;
using Framework.Database.Auth.Entities;
using Framework.Logging;
using Framework.Misc;

namespace AuthServer.Managers
{
    class RealmManager : Singleton<RealmManager>
    {
        public readonly ConcurrentDictionary<uint, realmlist> RealmList;

        RealmManager()
        {
            RealmList = new ConcurrentDictionary<uint, realmlist>();

            new Thread(() =>
            {
                while (true)
                {
                    Log.Message(LogType.Debug, "Updating RealmList...");
                    
                    realmlist realm;
                    var realms = DB.Auth.Select<realmlist>();

                    RealmList.ToList().ForEach(r =>
                    {
                        if (!realms.Any(nR => nR.Id.Equals(r.Key)))
                            if (RealmList.TryRemove(r.Key, out realm))
                                Log.Message(LogType.Debug, "Removed Realm (Id: {0}, Name: {1})", r.Key, r.Value.Name);
                    });

                    foreach (var r in realms)
                    {
                        if (RealmList.TryGetValue(r.Id, out realm))
                        {
                            RealmList.TryUpdate(r.Id, r, realm);
                            continue;
                        }

                        if (RealmList.TryAdd(r.Id, r))
                            Log.Message(LogType.Debug, "Added Realm (Id: {0}, Name: {1})", r.Id, r.Name);
                    }

                    IsInitialized = true;

                    if (RealmList.IsEmpty)
                        Log.Message(LogType.Debug, "No realms available");

                    Thread.Sleep(AuthConfig.RealmListUpdateTime);
                }
            }).Start();
        }
    }
}
