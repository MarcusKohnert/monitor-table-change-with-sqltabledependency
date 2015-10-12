﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oracle.DataAccess.Client;
using TableDependency.Enums;
using TableDependency.EventArgs;
using TableDependency.Mappers;
using TableDependency.OracleClient.IntegrationTest.Helpers;
using TableDependency.OracleClient.IntegrationTest.Model;

namespace TableDependency.OracleClient.IntegrationTest
{
    [TestClass]
    public class EventForAllColumns
    {
        private static readonly string ConnectionString = ConfigurationManager.ConnectionStrings["connectionString"].ConnectionString;
        private static readonly string TableName = ConfigurationManager.AppSettings.Get("tableName");
        private static int _counter = 0;
        private static Dictionary<string, Tuple<Item, Item>> _checkValues = new Dictionary<string, Tuple<Item, Item>>();

        [ClassInitialize()]
        public static void ClassInitialize(TestContext testContext)
        {
        }

        [TestInitialize()]
        public void TestInitialize()
        {
            using (var connection = new OracleConnection(ConnectionString))
            {
                connection.Open();
                using (var sqlCommand = connection.CreateCommand())
                {
                    sqlCommand.CommandText = "DELETE FROM " + TableName;
                    sqlCommand.ExecuteNonQuery();
                }
            }
        }

        [TestMethod]
        public void Test()
        {
            OracleTableDependency<Item> tableDependency = null;
            string naming = null;

            try
            {
                var mapper = new ModelToTableMapper<Item>();
                mapper.AddMapping(c => c.Description, "Long Description");

                tableDependency = new OracleTableDependency<Item>(ConnectionString, TableName, mapper);
                tableDependency.OnChanged += TableDependency_Changed;
                tableDependency.Start();
                naming = tableDependency.DataBaseObjectsNamingConvention;

                Thread.Sleep(5000);

                var t = new Task(ModifyTableContent);
                t.Start();
                t.Wait(30000);
            }
            finally
            {
                tableDependency?.Dispose();
            }

            Assert.AreEqual(_counter, 3);
            Assert.AreEqual(_checkValues[ChangeType.Insert.ToString()].Item2.Name, _checkValues[ChangeType.Insert.ToString()].Item1.Name);
            Assert.AreEqual(_checkValues[ChangeType.Insert.ToString()].Item2.Description, _checkValues[ChangeType.Insert.ToString()].Item1.Description);
            Assert.AreEqual(_checkValues[ChangeType.Delete.ToString()].Item2.Name, _checkValues[ChangeType.Delete.ToString()].Item1.Name);
            Assert.AreEqual(_checkValues[ChangeType.Delete.ToString()].Item2.Description, _checkValues[ChangeType.Delete.ToString()].Item1.Description);
            Assert.IsTrue(Helper.AreAllDbObjectDisposed(ConnectionString, naming));
        }

        private static void TableDependency_Changed(object sender, RecordChangedEventArgs<Item> e)
        {
            switch (e.ChangeType)
            {
                case ChangeType.Insert:
                    _counter++;
                    _checkValues[ChangeType.Insert.ToString()].Item2.Name = e.Entity.Name;
                    _checkValues[ChangeType.Insert.ToString()].Item2.Description = e.Entity.Description;
                    break;

                case ChangeType.Update:
                    _counter++;
                    _checkValues[ChangeType.Update.ToString()].Item2.Name = e.Entity.Name;
                    _checkValues[ChangeType.Update.ToString()].Item2.Description = e.Entity.Description;
                    break;

                case ChangeType.Delete:
                    _counter++;
                    _checkValues[ChangeType.Delete.ToString()].Item2.Name = e.Entity.Name;
                    _checkValues[ChangeType.Delete.ToString()].Item2.Description = e.Entity.Description;
                    break;
            }
        }

        private static void ModifyTableContent()
        {
            _checkValues.Add(ChangeType.Insert.ToString(), new Tuple<Item, Item>(new Item { Name = "Pizza Mergherita", Description = "Pizza Mergherita" }, new Item()));
            _checkValues.Add(ChangeType.Update.ToString(), new Tuple<Item, Item>(new Item { Name = "Pizza Funghi", Description = "Pizza Funghi" }, new Item()));
            _checkValues.Add(ChangeType.Delete.ToString(), new Tuple<Item, Item>(new Item { Name = "Pizza Funghi", Description = "Pizza Funghi" }, new Item()));

            using (var connection = new OracleConnection(ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"BEGIN INSERT INTO {TableName} (ID, NAME, \"Long Description\") VALUES ('1', '{_checkValues[ChangeType.Insert.ToString()].Item1.Name}', '{_checkValues[ChangeType.Insert.ToString()].Item1.Description}'); END;";
                    command.ExecuteNonQuery();
                    Thread.Sleep(2000);

                    command.CommandText = $"BEGIN UPDATE {TableName} SET NAME = '{_checkValues[ChangeType.Update.ToString()].Item1.Name}', \"Long Description\" = '{_checkValues[ChangeType.Update.ToString()].Item1.Description}'; END;";
                    command.ExecuteNonQuery();
                    Thread.Sleep(2000);

                    command.CommandText = $"BEGIN DELETE FROM {TableName}; END;";
                    command.ExecuteNonQuery();
                    Thread.Sleep(2000);
                }
            }
        }
    }
}