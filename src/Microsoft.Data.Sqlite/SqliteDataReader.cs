// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using Microsoft.Data.Sqlite.Interop;
using Microsoft.Data.Sqlite.Utilities;

namespace Microsoft.Data.Sqlite
{
    public class SqliteDataReader : DbDataReader
    {
        private static readonly IList<StatementHandle> _empty = new StatementHandle[0];

        private readonly SqliteCommand _command;
        private readonly bool _hasRows;
        private int _currentIndex;
        private StatementHandle _currentHandle;
        private IList<StatementHandle> _handles;
        private readonly int _recordsAffected;

        private bool _closed;
        private bool _hasRead;

        internal SqliteDataReader(SqliteCommand command, IList<StatementHandle> handles, int recordsAffected)
        {
            Debug.Assert(command != null, "command is null.");
            Debug.Assert(handles != null, "handles is null.");

            _command = command;

            if (handles.Count > 0)
            {
                _hasRows = true;
                _currentHandle = handles[0];
            }

            _handles = handles;
            _recordsAffected = recordsAffected;
        }

        public override int Depth
        {
            get { return 0; }
        }

        public override int FieldCount
        {
            get
            {
                CheckClosed("FieldCount");

                return NativeMethods.sqlite3_column_count(_currentHandle);
            }
        }

        public override bool HasRows
        {
            get { return _hasRows; }
        }

        public override bool IsClosed
        {
            get { return _closed; }
        }

        public override int RecordsAffected
        {
            get { return _recordsAffected; }
        }

        public override object this[string name]
        {
            get { return GetValue(GetOrdinal(name)); }
        }

        public override object this[int ordinal]
        {
            get { return GetValue(ordinal); }
        }

        public override IEnumerator GetEnumerator()
        {
            // TODO
            throw new NotImplementedException();
        }

        public override bool Read()
        {
            CheckClosed("Read");

            if (!_hasRead)
            {
                _hasRead = true;

                return NativeMethods.sqlite3_stmt_busy(_currentHandle) != 0;
            }

            Debug.Assert(_currentHandle != null && !_currentHandle.IsInvalid, "_currentHandle is null.");
            var rc = NativeMethods.sqlite3_step(_currentHandle);
            if (rc == Constants.SQLITE_DONE)
            {
                return false;
            }
            if (rc != Constants.SQLITE_ROW)
            {
                MarshalEx.ThrowExceptionForRC(rc);
            }

            return true;
        }

        public override bool NextResult()
        {
            CheckClosed("NextResult");

            _currentIndex++;

            if (_currentIndex >= _handles.Count)
            {
                return false;
            }

            _hasRead = false;
            _currentHandle = _handles[_currentIndex];

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (_closed || !disposing)
            {
                return;
            }

            Debug.Assert(_command.OpenReader == this, "_command.ActiveReader is not this.");

            if (_handles.Any())
            {
                foreach (var handle in _handles)
                {
                    if (handle != null
                        && !handle.IsInvalid)
                    {
                        var rc = NativeMethods.sqlite3_reset(handle);
                        MarshalEx.ThrowExceptionForRC(rc);
                    }
                }

                _handles = _empty;
            }

            _command.OpenReader = null;
            _closed = true;
        }

        public override string GetName(int ordinal)
        {
            CheckClosed("GetName");

            // TODO: Cache results #Perf
            return NativeMethods.sqlite3_column_name(_currentHandle, ordinal);
        }

        public override int GetOrdinal(string name)
        {
            CheckClosed("GetOrdinal");

            for (var i = 0; i < FieldCount; i++)
            {
                if (GetName(i) == name)
                {
                    return i;
                }
            }

            throw new IndexOutOfRangeException(name);
        }

        public override string GetDataTypeName(int ordinal)
        {
            CheckClosed("GetDataTypeName");

            return NativeMethods.sqlite3_column_decltype(_currentHandle, ordinal);
        }

        public override Type GetFieldType(int ordinal)
        {
            CheckClosed("GetFieldType");

            return GetTypeMap(ordinal).ClrType;
        }

        private SqliteTypeMap GetTypeMap(int ordinal)
        {
            return SqliteTypeMap.FromDeclaredType(GetDataTypeName(ordinal), GetSqliteType(ordinal));
        }

        private SqliteType GetSqliteType(int ordinal)
        {
            Debug.Assert(!_closed, "_closed is true.");

            return (SqliteType)NativeMethods.sqlite3_column_type(_currentHandle, ordinal);
        }

        public override bool IsDBNull(int ordinal)
        {
            CheckClosed("IsDBNull");

            return GetSqliteType(ordinal) == SqliteType.Null;
        }

        public override bool GetBoolean(int ordinal)
        {
            CheckClosed("GetBoolean");

            return GetFieldValue<bool>(ordinal);
        }

        public override byte GetByte(int ordinal)
        {
            CheckClosed("GetByte");

            return GetFieldValue<byte>(ordinal);
        }

        public override char GetChar(int ordinal)
        {
            CheckClosed("GetChar");

            return GetFieldValue<char>(ordinal);
        }

        public override DateTime GetDateTime(int ordinal)
        {
            CheckClosed("GetDateTime");

            return GetFieldValue<DateTime>(ordinal);
        }

        public override decimal GetDecimal(int ordinal)
        {
            CheckClosed("GetDecimal");

            return GetFieldValue<decimal>(ordinal);
        }

        public override double GetDouble(int ordinal)
        {
            CheckClosed("GetDouble");

            return GetFieldValue<double>(ordinal);
        }

        public override float GetFloat(int ordinal)
        {
            CheckClosed("GetFloat");

            return GetFieldValue<float>(ordinal);
        }

        public override Guid GetGuid(int ordinal)
        {
            CheckClosed("GetGuid");

            return GetFieldValue<Guid>(ordinal);
        }

        public override short GetInt16(int ordinal)
        {
            CheckClosed("GetInt16");

            return GetFieldValue<short>(ordinal);
        }

        public override int GetInt32(int ordinal)
        {
            CheckClosed("GetInt32");

            return GetFieldValue<int>(ordinal);
        }

        public override long GetInt64(int ordinal)
        {
            CheckClosed("GetInt64");

            return GetFieldValue<long>(ordinal);
        }

        public override string GetString(int ordinal)
        {
            CheckClosed("GetString");

            return GetFieldValue<string>(ordinal);
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            throw new NotSupportedException();
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            throw new NotSupportedException();
        }

        public override T GetFieldValue<T>(int ordinal)
        {
            CheckClosed("GetFieldValue");

            if (typeof(T) == typeof(object))
            {
                return (T)GetValue(ordinal);
            }

            var map = SqliteTypeMap.FromClrType<T>();
            var value = ColumnReader.Read(map.SqliteType, _currentHandle, ordinal);

            return (T)map.FromInterop(value);
        }

        public override object GetValue(int ordinal)
        {
            CheckClosed("GetValue");

            if (IsDBNull(ordinal))
            {
                return DBNull.Value;
            }

            var map = GetTypeMap(ordinal);
            var value = ColumnReader.Read(map.SqliteType, _currentHandle, ordinal);

            return map.FromInterop(value);
        }

        public override int GetValues(object[] values)
        {
            CheckClosed("GetValues");

            for (var i = 0; i < FieldCount; i++)
            {
                values[i] = GetValue(i);
            }

            return FieldCount;
        }

        private void CheckClosed(string operation)
        {
            if (_closed)
            {
                throw new InvalidOperationException(Strings.FormatDataReaderClosed(operation));
            }
        }
    }
}
