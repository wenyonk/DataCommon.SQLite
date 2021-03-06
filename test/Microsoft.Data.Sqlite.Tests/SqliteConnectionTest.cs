// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.IO;
using Xunit;

namespace Microsoft.Data.Sqlite
{
    public class SqliteConnectionTest
    {
        [Fact]
        public void Ctor_validates_argument()
        {
            var ex = Assert.Throws<ArgumentException>(() => new SqliteConnection(null));

            Assert.Equal(Strings.FormatArgumentIsNullOrWhitespace("connectionString"), ex.Message);
        }

        [Fact]
        public void Ctor_sets_connection_string()
        {
            using (var connection = new SqliteConnection("Filename=test.db"))
            {
                Assert.Equal("Filename=test.db", connection.ConnectionString);
            }
        }

        [Fact]
        public void ConnectionString_setter_validates_argument()
        {
            using (var connection = new SqliteConnection())
            {
                var ex = Assert.Throws<ArgumentException>(() => connection.ConnectionString = null);

                Assert.Equal(Strings.FormatArgumentIsNullOrWhitespace("value"), ex.Message);
            }
        }

        [Fact]
        public void ConnectionString_setter_throws_when_open()
        {
            using (var connection = new SqliteConnection("Filename=:memory:"))
            {
                connection.Open();

                var ex = Assert.Throws<InvalidOperationException>(
                    () => connection.ConnectionString = "Filename=test.db");

                Assert.Equal(Strings.ConnectionStringRequiresClosedConnection, ex.Message);
            }
        }

        [Fact]
        public void ConnectionString_gets_and_sets_value()
        {
            using (var connection = new SqliteConnection { ConnectionString = "Filename=test.db" })
            {
                Assert.Equal("Filename=test.db", connection.ConnectionString);
            }
        }

        [Fact]
        public void Database_returns_value()
        {
            using (var connection = new SqliteConnection())
            {
                Assert.Equal("main", connection.Database);
            }
        }

        [Fact]
        public void DataSource_returns_connection_string_filename_when_closed()
        {
            using (var connection = new SqliteConnection("Filename=test.db"))
            {
                Assert.Equal("test.db", connection.DataSource);
            }
        }

        [Fact]
        public void DataSource_returns_actual_filename_when_open()
        {
            using (var connection = new SqliteConnection("Filename=test.db"))
            {
                connection.Open();

                var result = connection.DataSource;

                Assert.True(Path.IsPathRooted(result));
                Assert.Equal("test.db", Path.GetFileName(result));
            }
        }

        [Fact]
        public void ServerVersion_returns_value()
        {
            using (var connection = new SqliteConnection())
            {
                var version = connection.ServerVersion;

                Assert.NotNull(version);
                Assert.True(version.StartsWith("3."));
            }
        }

        [Fact]
        public void State_closed_by_default()
        {
            using (var connection = new SqliteConnection())
            {
                Assert.Equal(ConnectionState.Closed, connection.State);
            }
        }

        [Fact]
        public void Open_can_be_called_when_disposed()
        {
            using (var connection = new SqliteConnection("Filename=:memory:"))
            {
                connection.Dispose();

                connection.Open();
            }
        }

        [Fact]
        public void Open_throws_when_no_connection_string()
        {
            using (var connection = new SqliteConnection())
            {
                var ex = Assert.Throws<InvalidOperationException>(() => connection.Open());

                Assert.Equal(Strings.OpenRequiresSetConnectionString, ex.Message);
            }
        }

        [Fact]
        public void Open_throws_when_error()
        {
            using (var connection = new SqliteConnection("Filename=:*?\"<>|"))
            {
                var ex = Assert.Throws<SqliteException>(() => connection.Open());

                Assert.Equal(14, ex.SqliteErrorCode);
            }
        }

        [Fact]
        public void Open_works()
        {
            using (var connection = new SqliteConnection("Filename=:memory:"))
            {
                var raised = false;
                StateChangeEventHandler handler = (sender, e) =>
                    {
                        raised = true;

                        Assert.Equal(connection, sender);
                        Assert.Equal(ConnectionState.Closed, e.OriginalState);
                        Assert.Equal(ConnectionState.Open, e.CurrentState);
                    };

                connection.StateChange += handler;
                try
                {
                    connection.Open();

                    Assert.True(raised);
                    Assert.Equal(ConnectionState.Open, connection.State);
                }
                finally
                {
                    connection.StateChange -= handler;
                }
            }
        }

        [Fact]
        public void Open_can_be_called_more_than_once()
        {
            using (var connection = new SqliteConnection("Filename=:memory:"))
            {
                connection.Open();
                connection.Open();
            }
        }

        [Fact]
        public void Close_works()
        {
            using (var connection = new SqliteConnection("Filename=:memory:"))
            {
                connection.Open();

                var raised = false;
                StateChangeEventHandler handler = (sender, e) =>
                    {
                        raised = true;

                        Assert.Equal(connection, sender);
                        Assert.Equal(ConnectionState.Open, e.OriginalState);
                        Assert.Equal(ConnectionState.Closed, e.CurrentState);
                    };

                connection.StateChange += handler;
                try
                {
                    connection.Close();

                    Assert.True(raised);
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
                finally
                {
                    connection.StateChange -= handler;
                }
            }
        }

        [Fact]
        public void Close_can_be_called_before_open()
        {
            using (var connection = new SqliteConnection("Filename=test.db"))
            {
                connection.Close();
            }
        }

        [Fact]
        public void Close_can_be_called_more_than_once()
        {
            using (var connection = new SqliteConnection("Filename=:memory:"))
            {
                connection.Open();
                connection.Close();
                connection.Close();
            }
        }

        [Fact]
        public void Dispose_closes_connection()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            var raised = false;
            StateChangeEventHandler handler = (sender, e) =>
                {
                    raised = true;

                    Assert.Equal(connection, sender);
                    Assert.Equal(ConnectionState.Open, e.OriginalState);
                    Assert.Equal(ConnectionState.Closed, e.CurrentState);
                };

            connection.StateChange += handler;
            try
            {
                connection.Dispose();

                Assert.True(raised);
                Assert.Equal(ConnectionState.Closed, connection.State);
            }
            finally
            {
                connection.StateChange -= handler;
            }
        }

        [Fact]
        public void Dispose_can_be_called_more_than_once()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            connection.Dispose();
            connection.Dispose();
        }

        [Fact]
        public void CreateCommand_returns_command()
        {
            using (var connection = new SqliteConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    Assert.NotNull(command);
                    Assert.Same(connection, command.Connection);
                }
            }
        }

        [Fact]
        public void BeginTransaction_throws_when_closed()
        {
            using (var connection = new SqliteConnection())
            {
                var ex = Assert.Throws<InvalidOperationException>(() => connection.BeginTransaction());

                Assert.Equal(Strings.FormatCallRequiresOpenConnection("BeginTransaction"), ex.Message);
            }
        }

        [Fact]
        public void BeginTransaction_throws_when_parallel_transaction()
        {
            using (var connection = new SqliteConnection("Filename=:memory:"))
            {
                connection.Open();

                using (connection.BeginTransaction())
                {
                    var ex = Assert.Throws<InvalidOperationException>(() => connection.BeginTransaction());

                    Assert.Equal(Strings.ParallelTransactionsNotSupported, ex.Message);
                }
            }
        }

        [Fact]
        public void BeginTransaction_works()
        {
            using (var connection = new SqliteConnection("Filename=:memory:"))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    Assert.NotNull(transaction);
                    Assert.Same(transaction, connection.Transaction);
                    Assert.Equal(connection, transaction.Connection);
                    Assert.Equal(IsolationLevel.Serializable, transaction.IsolationLevel);
                }
            }
        }

        [Fact]
        public void ChangeDatabase_not_supported()
        {
            using (var connection = new SqliteConnection())
            {
                Assert.Throws<NotSupportedException>(() => connection.ChangeDatabase("new"));
            }
        }
    }
}
