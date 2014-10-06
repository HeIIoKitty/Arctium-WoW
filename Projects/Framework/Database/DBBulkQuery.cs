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
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Framework.Attributes;
using Framework.Misc;

namespace Framework.Database
{
    public class DBBulkQuery<T>
    {
        public StringBuilder Query { get; set; }
        List<PropertyInfo> properties;
        ConcurrentDictionary<long, T> values;

        public DBBulkQuery()
        {
            Query = new StringBuilder();

            values = new ConcurrentDictionary<long, T>();
            properties = new List<PropertyInfo>();

            Query.AppendFormat("INSERT INTO `{0}` (", typeof(T).Name);

            foreach (var p in typeof(T).GetProperties())
            {
                var attr = p.GetCustomAttribute<FieldAttribute>();

                if (p.GetMethod.IsVirtual || (attr != null && attr.AutoIncrement))
                    continue;

                properties.Add(p);

                Query.AppendFormat("`{0}`,", p.Name);
            }

            Query.Append(") VALUES ");
        }

        public void Add(long key, T entity)
        {
            values.TryAdd(key, entity);
        }

        public bool Finish(Database db)
        {
            foreach (var entity in values)
            {
                Query.Append("(");

                foreach (var p in properties)
                    Query.Append(string.Format("'{0}', ", p.GetValue(entity.Value)));

                Query.Append("),");
            }

            Query.Replace(", )", ")");
            Query.Replace(",)", ")");
            Query.Remove(Query.Length - 1, 1);
            Query.Append(";");

            return db.Execute(Query.ToString());
        }
    }
}
