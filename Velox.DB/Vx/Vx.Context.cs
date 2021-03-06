#region License
//=============================================================================
// Velox.DB - Portable .NET ORM 
//
// Copyright (c) 2015 Philippe Leybaert
//
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal 
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//=============================================================================
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Velox.DB.Core;

namespace Velox.DB
{
    public static partial class Vx
    {
        public class Context : IDisposable
        {
            private readonly SafeDictionary<Type, Repository> _repositories = new SafeDictionary<Type, Repository>();

            public IDataProvider DataProvider { get; private set; }

            public Context(IDataProvider dataProvider)
            {
                DataProvider = dataProvider;

                foreach (var property in GetType().Inspector().GetFieldsAndProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var fieldTypeInspector = property.Type.Inspector();

                    if (!fieldTypeInspector.IsGenericType || property.Type.GetGenericTypeDefinition() != typeof (IDataSet<>))
                        continue;

                    var objectType = fieldTypeInspector.GetGenericArguments()[0];
                    var repository = (Repository) Activator.CreateInstance(typeof (Repository<>).MakeGenericType(objectType), this);
                    var dataSet = Activator.CreateInstance(typeof (DataSet<>).MakeGenericType(objectType), repository);

                    _repositories[objectType] = repository;

                    property.SetValue(this, dataSet);
                }

                GenerateRelations();

                if (DB == null)
                    DB = this;
            }

            private void GenerateRelations()
            {
                Repository repository;

                while ((repository = _repositories.Values.FirstOrDefault(rep => rep.Schema.Relations == null)) != null)
                    repository.Schema.UpdateRelations();
 
                foreach (var repo in _repositories.Values)
                    repo.Schema.UpdateReverseRelations();
            }

            internal OrmSchema GetSchema(Type objectType, bool autoCreate = true)
            {
                var repository = _repositories[objectType];

                if (repository == null)
                {
                    if (!autoCreate)
                        return null;

                    _repositories[objectType] = (repository = (Repository) Activator.CreateInstance(typeof (Repository<>).MakeGenericType(objectType), this));

                    GenerateRelations();
                }

                return repository.Schema;
            }

            private Repository<T> GetRepository<T>()
            {
                var repository = _repositories[typeof(T)];

                if (repository == null)
                {
                    _repositories[typeof(T)] = (repository = new Repository<T>(this));

                    GenerateRelations();
                }

                return (Repository<T>) repository;
            }

            public IDataSet<T> DataSet<T>()
            {
                return new DataSet<T>(GetRepository<T>());
            }

            public void CreateTable<T>(bool recreateTable = false, bool recreateIndexes = false)
            {
                DataProvider.CreateOrUpdateTable(GetRepository<T>().Schema, recreateTable, recreateIndexes);
            }

            public T Read<T>(object key, params Expression<Func<T, object>>[] relationsToLoad)
            {
                return GetRepository<T>().Read(key, relationsToLoad);
            }

            public T Read<T>(Expression<Func<T,bool>> condition,  params Expression<Func<T, object>>[] relationsToLoad)
            {
                return DataSet<T>().WithRelations(relationsToLoad).FirstOrDefault(condition);
            }

            public T Load<T>(T obj, object key, params Expression<Func<T, object>>[] relationsToLoad)
            {
                return GetRepository<T>().Load(obj, key, relationsToLoad);
            }

            public bool Save<T>(T obj, bool saveRelations = false, bool? create = null)
            {
                return GetRepository<T>().Save(obj, saveRelations, create: create);
            }

            public bool InsertOrUpdate<T>(T obj, bool saveRelations = false)
            {
                return GetRepository<T>().Save(obj, saveRelations, create: null);
            }

            public bool Update<T>(T obj, bool saveRelations = false)
            {
                return GetRepository<T>().Save(obj, saveRelations, create: false);
            }

            public bool Insert<T>(T obj, bool saveRelations = false)
            {
                return GetRepository<T>().Save(obj, saveRelations, create: true);
            }

            public bool Delete<T>(T obj)
            {
                return GetRepository<T>().Delete(obj);
            }

            public bool Delete<T>(Expression<Func<T, bool>> condition)
            {
                var repository = GetRepository<T>();

                return repository.Delete(repository.CreateQuerySpec(new FilterSpec(condition)));
            }

            public int Execute(string sql, object parameters)
            {
                return DataProvider.ExecuteSql(sql, new QueryParameterCollection(parameters));
            }

            public IEnumerable<T> Query<T>(string sql, object parameters = null) where T : new()
            {
                return DataProvider.Query(sql, new QueryParameterCollection(parameters)).Select(entity => entity.CreateObject<T>());
            }

            public IEnumerable<Dictionary<string,object>> Query(string sql, object parameters = null)
            {
                return DataProvider.Query(sql, new QueryParameterCollection(parameters)).Select(entity => entity.AsDictionary());
            }

            public T QueryScalar<T>(string sql, object parameters = null) where T : new()
            {
                return DataProvider.QueryScalar(sql, new QueryParameterCollection(parameters)).FirstOrDefault().Convert<T>();
            }

            public IEnumerable<T> QueryScalars<T>(string sql, object parameters = null) where T : new()
            {
                return DataProvider.QueryScalar(sql, new QueryParameterCollection(parameters)).Select(scalar => scalar.Convert<T>());
            }

            // Async methods

            public IAsyncDataSet<T> AsyncDataSet<T>()
            {
                return new AsyncDataSet<T>(new DataSet<T>(GetRepository<T>()));
            }

            public Task CreateTableAsync<T>(bool recreateTable = false, bool recreateIndexes = false)
            {
                return Task.Factory.StartNew(() => CreateTable<T>(recreateTable, recreateIndexes));
            }

            public Task<int> ExecuteAsync(string sql, object parameters)
            {
                return Task.Factory.StartNew(() => Execute(sql, parameters));
            }

            public Task<T[]> QueryAsync<T>(string sql, object parameters = null) where T : new()
            {
                return Task.Factory.StartNew(() => Query<T>(sql, parameters).ToArray());
            }

            public Task<T> QueryScalarAsync<T>(string sql, object parameters = null) where T : new()
            {
                return Task.Factory.StartNew(() => QueryScalar<T>(sql, parameters));
            }

            public Task<T[]> QueryScalarsAsync<T>(string sql, object parameters = null) where T : new()
            {
                return Task.Factory.StartNew(() => QueryScalars<T>(sql, parameters).ToArray());
            }

            public Task<T> ReadAsync<T>(object key, params Expression<Func<T, object>>[] relationsToLoad)
            {
                return Task.Factory.StartNew(() => GetRepository<T>().Read(key, relationsToLoad));
            }

            public Task<T> ReadAsync<T>(Expression<Func<T, bool>> condition, params Expression<Func<T, object>>[] relationsToLoad)
            {
                return AsyncDataSet<T>().WithRelations(relationsToLoad).FirstOrDefault(condition);
            }

            public Task<T> LoadAsync<T>(T obj, object key, params Expression<Func<T, object>>[] relationsToLoad)
            {
                return Task.Factory.StartNew(() => GetRepository<T>().Load(obj, key, relationsToLoad));
            }

            public Task<bool> SaveAsync<T>(T obj, bool saveRelations = false, bool? create = null)
            {
                return Task.Factory.StartNew(() => GetRepository<T>().Save(obj, saveRelations, create));
            }

            public Task<bool> InsertAsync<T>(T obj, bool saveRelations = false)
            {
                return Task.Factory.StartNew(() => GetRepository<T>().Save(obj, saveRelations, true));
            }

            public Task<bool> UpdateAsync<T>(T obj, bool saveRelations = false)
            {
                return Task.Factory.StartNew(() => GetRepository<T>().Save(obj, saveRelations, false));
            }

            public Task<bool> DeleteAsync<T>(T obj)
            {
                return Task.Factory.StartNew(() => GetRepository<T>().Delete(obj));
            }

            // IDisposable

            public void Dispose()
            {
                DataProvider.Dispose();

                DataProvider = null;
            }
        }
    }
}