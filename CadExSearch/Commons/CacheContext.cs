using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

#nullable disable

namespace CadExSearch.Commons
{
    public static class CacheContext
    {
        static CacheContext()
        {
            using var _ = GlobalLock.LockSync();
            using var con = CreateConnection();
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText =
                    @"create table if not exists 'Record' ('ID' text not null unique primary key,'Content' text not null,'BaseID' text not null default('subject'),constraint 'FK_Record_Record_BaseID' foreign key('BaseID') references 'Record'('ID') on delete cascade);";
                cmd.ExecuteNonQuery();
            }

            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "create index if not exists 'IX_Record_BaseID' on 'Record'('BaseID');";
                cmd.ExecuteNonQuery();
            }

            int count;
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "select count(*) from Record";
                count = cmd.ExecuteScalar() switch {null => 0, int x => x, long x => (int) x, _ => 0};
            }

            if (count != 0) return;
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "insert into Record (ID,Content,BaseID) values ('subject','root_record','subject');";
                cmd.ExecuteNonQuery();
            }
        }

        private static SHA512 Sha { get; } = SHA512.Create();

        private static async Task<string> GetID(string data)
        {
            await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(data));
            var hash = Convert.ToBase64String(await Sha.ComputeHashAsync(ms));
            return Regex.Replace(hash, @"(\W|\d)", "").ToUpper()[..20];
        }

        private static SQLiteConnection CreateConnection()
        {
            return new SQLiteConnection(
                    "Data Source =.\\CEOCC.Cache.db3;PRAGMA synchronous=FULL;journal_mode=WAL;count_changes=OFF;temp_store=MEMORY;page_size=65536;cache_size=-16777216;")
                .OpenAndReturn();
        }

        public static async Task<string[]> CacheGetValues(string root)
        {
            await using var _ = await GlobalLock.LockAsync();
            await using var con = CreateConnection();
            await using var cmd = con.CreateCommand();

            cmd.CommandText =
                $"select * from Record where BaseID='{root}' and not Content='root_record' and not Content='fork_record';";
            var reader = await cmd.ExecuteReaderAsync();
            var ret = new List<string>();
            while (await reader.ReadAsync()) ret.Add(reader.GetString(1));Searc

            return ret.ToArray();
        }

        public static async Task CacheSetValues(string root, params string[] recs)
        {
            await using var _ = await GlobalLock.LockAsync();
            await using var con = CreateConnection();
            await using var tran = await con.BeginTransactionAsync();

            foreach (var rec in recs)
            {
                var id = GetID(rec);
                await using var cmd = con.CreateCommand();
                cmd.CommandText =
                    $"insert into Record(ID,Content,BaseID) values ('{id}','{rec}','{root}') on conflict (ID) do nothing;";
                cmd.ExecuteNonQuery();
            }

            await tran.CommitAsync();
        }

        public static async Task CacheSetValues(string root, IEnumerable<string> recs)
        {
            await using var _ = await GlobalLock.LockAsync();
            await using var con = CreateConnection();
            await using var tran = await con.BeginTransactionAsync();

            foreach (var rec in recs)
            {
                var id = GetID(rec);
                await using var cmd = con.CreateCommand();
                cmd.CommandText =
                    $"insert into Record(ID,Content,BaseID) values ('{id}','{rec}','{root}') on conflict (ID) do nothing;";
                cmd.ExecuteNonQuery();
            }

            await tran.CommitAsync();
        }

        public static async Task AddFork(string root, string fork)
        {
            await using var _ = await GlobalLock.LockAsync();
            await using var con = CreateConnection();

            await using var cmd = con.CreateCommand();
            cmd.CommandText =
                $"insert into Record(ID,Content,BaseID) values ('{fork}','fork_record','{root}') on conflict (ID) do nothing;";
            cmd.ExecuteNonQuery();
        }
    }
}