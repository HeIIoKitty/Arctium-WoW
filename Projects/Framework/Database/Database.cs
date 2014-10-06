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
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Framework.Attributes;
using Framework.Misc;
using MySql.Data.MySqlClient;

namespace Framework.Database
{
    public class Database : IDisposable
    {
        MySqlConnection connection;

        public bool CreateConnection(string host, string user, string password, string database, int port, bool pooling, int minPoolSize, int maxPoolSize)
        {
            var pools = string.Format(";Min Pool Size={0};Max Pool Size={1}", minPoolSize, maxPoolSize);

            var connectionString = "Server=" + host + ";User Id=" + user + ";Port=" + port + ";" +
                                   "Password=" + password + ";Database=" + database + ";Allow Zero Datetime=True;" +
                                   "Pooling=" + pooling + ";CharSet=utf8";

            if (pooling)
                connectionString += pools;

            try
            {
                connection = new MySqlConnection(connectionString);

                connection.Open();
            }
            catch
            {
            }

            return connection.State == ConnectionState.Open;
        }

        public bool Execute(string sql, params object[] args)
        {
            var sqlString = new StringBuilder();

            // Fix for floating point problems on some languages
            sqlString.AppendFormat(CultureInfo.GetCultureInfo("en-US").NumberFormat, sql);

            try
            {
                using (var sqlCommand = new MySqlCommand(sqlString.ToString(), connection))
                {
                    var mParams = new List<MySqlParameter>(args.Length);

                    foreach (var a in args)
                        mParams.Add(new MySqlParameter("", a));

                    sqlCommand.Parameters.AddRange(mParams.ToArray());

                    sqlCommand.ExecuteNonQuery();
                }

                return true;
            }
            catch
            {
            }

            return false;
        }

        public SQLResult Select(string sql, params object[] args)
        {
            var sqlString = new StringBuilder();

            // Fix for floating point problems on some languages
            sqlString.AppendFormat(CultureInfo.GetCultureInfo("en-US").NumberFormat, sql);

            try
            {
                using (var sqlCommand = new MySqlCommand(sqlString.ToString(), connection))
                {
                    var mParams = new List<MySqlParameter>(args.Length);

                    foreach (var a in args)
                        mParams.Add(new MySqlParameter("", a));

                    sqlCommand.Parameters.AddRange(mParams.ToArray());

                    using (var SqlData = sqlCommand.ExecuteReader(CommandBehavior.Default))
                    {
                        if (SqlData.HasRows)
                        {
                            using (var retData = new SQLResult())
                            {

                                retData.Load(SqlData);
                                retData.Count = retData.Rows.Count;

                                return retData;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public bool Add<T>(T entity) where T : class
        {
            var properties = typeof(T).GetProperties();
            var values = new Dictionary<string, object>(properties.Length);
            var queryBuilder = new StringBuilder();

            for (var i = 0; i < properties.Length; i++)
            {
                var p = properties[i];
                var attr = p.GetCustomAttribute<FieldAttribute>();

                if (attr != null && attr.AutoIncrement)
                    continue;

                var pValue = p.GetValue(entity);

                if (pValue != null)
                    values.Add(p.Name, pValue);
            }

            queryBuilder.AppendFormat("INSERT INTO `{0}` (", typeof(T).Name);

            foreach (var name in values.Keys)
                queryBuilder.AppendFormat("`{0}`,", name);

            queryBuilder.Append(") VALUES (");

            for (var i = 0; i < values.Count; i++)
                queryBuilder.Append("?,");

            queryBuilder.Append(")");

            var query = queryBuilder.ToString().Replace(",)", ")");

            return Execute(query, values.Values.ToArray());
        }

        public bool AddBulk<T>(IList<T> entities) where T : class
        {
            var properties = typeof(T).GetProperties();
            var queryBuilder = new StringBuilder();

            queryBuilder.AppendFormat("INSERT INTO `{0}` (", typeof(T).Name);

            foreach (var p in properties)
            {
                var attr = p.GetCustomAttribute<FieldAttribute>();

                if (attr != null && attr.AutoIncrement)
                    continue;

                queryBuilder.AppendFormat("`{0}`,", p.Name);
            }

            queryBuilder.Append(") VALUES ");

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];

                queryBuilder.Append("(");

                foreach (var p in properties)
                {
                    var attr = p.GetCustomAttribute<FieldAttribute>();

                    if (attr != null && attr.AutoIncrement)
                        continue;

                    var pValue = p.GetValue(entity);

                    queryBuilder.Append(string.Format("'{0}', ", pValue));
                }

                if (i == entities.Count - 1)
                    queryBuilder.AppendLine(");");
                else
                    queryBuilder.AppendLine("),");
            }

            var query = queryBuilder.Replace(", )", ")");

            return Execute(query.ToString());
        }

        public bool Update<T>(T entity, params object[] values) where T : new()
        {
            var pkData = GetForeignKeyBaseData(entity);
            var query = new StringBuilder();

            query.AppendFormat("UPDATE `{0}` SET ", typeof(T).Name);

            for (var i = 0; i < values.Length; i += 2)
                query.AppendFormat("`{0}` = '{1}', ", values[i].ToString(), values[i + 1]);

            query.AppendFormat("WHERE `{0}` = '{1}'", "Id", pkData.Item2);
            
            return Execute(query.Replace(", WHERE", " WHERE").ToString());
        }

        public bool Delete<T>(Expression<Func<T, object>> expression) where T : class
        {
            var bExpression = (expression.Body as UnaryExpression).Operand as BinaryExpression;

            if (bExpression == null)
                throw new NotSupportedException("Only BinaryExpressions are supported.");

            var builder = new QueryBuilder();

            // We support only 1 table for now
            var query = builder.BuildDelete<T>(bExpression, expression.Parameters[0].Name);

            return Execute(query);
        }

        public List<T> Select<T>() where T : new()
        {
            var builder = new QueryBuilder();

            // We support only 1 table for now
            var query = builder.BuildSelect<T>();
            var properties = typeof(T).GetProperties().Where(p => !p.GetMethod.IsVirtual).ToArray();
            var foreignKeys = typeof(T).GetProperties().Where(p => p.GetMethod.IsVirtual).ToArray();
            var entities = new List<T>();

            if ((var data = Select(query)) != null)
            {
                if (data.Columns.Count != properties.Count())
                    throw new NotSupportedException("Columns doesn't match the entity fields.");

                if (data.Count == 0)
                    return default(List<T>);

                for (var i = 0; i < data.Count; i++)
                {
                    var entity = new T();

                    for (var j = 0; j < properties.Length; j++)
                    {
                        var p = properties[j];
                        var val = data.Rows[i][p.Name];
                        
                        p.SetValue(entity, val.ChangeType(p.PropertyType), null);
                    }

                    AssignForeignKeyData(entity, foreignKeys);

                    entities.Add(entity);
                }
            }

            return entities;
        }

        public T Single<T>(Expression<Func<T, object>> expression) where T : new()
        {
            var bExpression = (expression.Body as UnaryExpression).Operand as BinaryExpression;
            var builder = new QueryBuilder();

            // We support only 1 table for now
            var query = builder.BuildWhere<T>(bExpression, expression.Parameters[0].Name);

            if ((var data = Select(query)) != null)
            {
                if (data.Count > 1)
                    throw new NotSupportedException("Result contains more than 1 element.");

                if (data.Count == 0)
                    return default(T);

                var properties = typeof(T).GetProperties().Where(p => !p.GetMethod.IsVirtual).ToArray();
                var foreignKeys = typeof(T).GetProperties().Where(p => p.GetMethod.IsVirtual).ToArray();

                if (data.Columns.Count != properties.Length)
                    throw new NotSupportedException("Columns doesn't match the entity fields.");

                var entity = new T();

                for (var i = 0;  i < properties.Length; i++)
                {
                    var p = properties[i];
                    var val = data.Rows[0][p.Name];

                    p.SetValue(entity, val.ChangeType(p.PropertyType), null);
                }

                AssignForeignKeyData(entity, foreignKeys);

                return entity;
            }

            return default(T);
        }

        public List<T> Where<T>(Expression<Func<T, object>> expression) where T : new()
        {
            var bExpression = (expression.Body as UnaryExpression).Operand as BinaryExpression;
            var builder = new QueryBuilder();

            // We support only 1 table for now
            var query = builder.BuildWhere<T>(bExpression, expression.Parameters[0].Name);
            var entities = new List<T>();

            if ((var data = Select(query)) != null)
            {
                var properties = typeof(T).GetProperties().Where(p => !p.GetMethod.IsVirtual).ToArray();
                var foreignKeys = typeof(T).GetProperties().Where(p => p.GetMethod.IsVirtual).ToArray();

                if (data.Columns.Count != properties.Length)
                    throw new NotSupportedException("Columns doesn't match the entity fields.");

                for (var i = 0;  i < data.Count; i++)
                {
                    var entity = new T();

                    for (var j = 0; j < properties.Length; j++)
                    {
                        var p = properties[j];
                        var val = data.Rows[i][p.Name];

                        p.SetValue(entity, val.ChangeType(p.PropertyType), null);
                    }

                    AssignForeignKeyData(entity, foreignKeys);

                    entities.Add(entity);
                }
            }

            return entities;
        }

        public bool Any<T>(Expression<Func<T, object>> expression) where T : new()
        {
            var bExpression = (expression.Body as UnaryExpression).Operand as BinaryExpression;
            var builder = new QueryBuilder();

            // We support only 1 table for now
            var query = builder.BuildWhere<T>(bExpression, expression.Parameters[0].Name);

            return Select(query) != null;
        }

        IList Where<T>(T baseEntity, Type entityType, string query)
        {
            if ((var data = Select(query)) != null)
            {
                var properties = entityType.GetProperties().Where(p => !p.GetMethod.IsVirtual).ToArray();
                var localBaseEntity = entityType.GetProperties().SingleOrDefault(p => p.GetMethod.IsVirtual && !p.PropertyType.IsGenericType);
                var foreignKeys = entityType.GetProperties().Where(p => p.GetMethod.IsVirtual && p.PropertyType.IsGenericType).ToArray();

                if (data.Columns.Count != properties.Length)
                    throw new NotSupportedException("Columns doesn't match the entity fields.");

                if (data.Count == 0)
                    return default(List<T>);

                var entities = entityType.CreateList();

                for (var i = 0; i < data.Count; i++)
                {
                    var entity = Activator.CreateInstance(entityType);
                    Tuple<string, object> pFkData = null;

                    for (var j = 0; j < properties.Length; j++)
                    {
                        var p = properties[j];
                        var val = data.Rows[i][p.Name];

                        if ((var attr = p.GetCustomAttribute<FieldAttribute>()) != null && attr.PrimaryKey)
                            pFkData = Tuple.Create(p.Name, val);
                        else if (p.Name == "Id")
                            pFkData = Tuple.Create(entityType.Name + p.Name, val);

                        p.SetValue(entity, val.ChangeType(p.PropertyType), null);
                    }

                    // Check if we have a IList here
                    if (localBaseEntity != null && !localBaseEntity.PropertyType.IsGenericType)
                        localBaseEntity.SetValue(entity, baseEntity, null);

                    AssignForeignKeyData(entity, foreignKeys, pFkData);

                    entities.Add(entity);
                }

                return entities;
            }


            return default(IList);
        }

        Tuple<string, object> GetForeignKeyBaseData<T>(T entity)
        {
            var type = typeof(T);

            if ((var pKey = type.GetProperty("Id")) != null)
            {
                var pKeyData = pKey.GetValue(entity);

                return Tuple.Create(type.Name.Singularize() + "Id", pKeyData); 
            }

            return null;
        }

        void AssignForeignKeyData<T>(T entity, PropertyInfo[] foreignKeys, Tuple<string, object> pFkData = null)
        {
            for (var j = 0; j < foreignKeys.Length; j++)
            {
                var p = foreignKeys[j];
                var type = p.PropertyType.IsGenericType ? p.PropertyType.GetGenericArguments()[0] : p.PropertyType;
                var attr = p.GetCustomAttribute<FieldAttribute>();
                var query = "";

                if (attr != null && attr.ForeignKey != "")
                {
                    var baseData = GetForeignKeyBaseData(entity);
                    var pKey = typeof(T).GetProperty(attr.ForeignKey);
                    var pKeyData = pKey != null ? pKey.GetValue(entity) : (baseData != null ? baseData.Item2 : null);

                    if (pKeyData != null)
                        query = string.Format("SELECT * FROM {0} WHERE {1} = '{2}'", type.Name, attr.ForeignKey, pKeyData);
                }
                else
                {
                    var fkBaseData = pFkData ?? GetForeignKeyBaseData(entity);

                    if (fkBaseData != null)
                        query = string.Format("SELECT * FROM {0} WHERE {1} = '{2}'", type.Name, p.PropertyType.IsGenericType ? fkBaseData.Item1 : "Id", fkBaseData.Item2);
                }

                if (query != "")
                {
                    var fkData = Where(entity, type, query);

                    p.SetValue(entity, p.PropertyType.IsGenericType ? fkData : fkData[0], null);
                }
            }
        }

        public void Dispose()
        {
            connection.Dispose();
        }
    }
}
