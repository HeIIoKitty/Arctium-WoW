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

using System.Collections.Generic;
using Framework.Constants.Account;

namespace Framework.Database.Auth.Entities
{
    public class GameAccounts
    {
        public uint Id                { get; set; }
        public uint AccountId         { get; set; }
        public string Game            { get; set; }
        public byte Index             { get; set; }
        public Regions Region         { get; set; }
        public GameAccountFlags Flags { get; set; }
        public byte BoxLevel          { get; set; }
        public string OS              { get; set; }
        public string SessionKey      { get; set; }
        public bool IsOnline          { get; set; }

        public virtual Accounts Account { get; set; }
    }
}
