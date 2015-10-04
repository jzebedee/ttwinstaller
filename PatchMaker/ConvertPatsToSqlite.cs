using System;
using System.Data.SQLite;
using System.IO;
using Patching.Delta;
using TaleOfTwoWastelands.Patching;

namespace PatchMaker
{
    internal static class ConvertPatsToSqlite
    {
        private const string SchemaSetup = @"BEGIN TRANSACTION;
CREATE TABLE `Patches` (
	`id`	INTEGER NOT NULL UNIQUE,
	`hashId`	INTEGER NOT NULL,
	`key`	TEXT NOT NULL UNIQUE,
	PRIMARY KEY(id),
	FOREIGN KEY(`hashId`) REFERENCES Hashes_id
);
CREATE TABLE `PatchData` (
	`id`	INTEGER NOT NULL UNIQUE,
	`patchId`	INTEGER NOT NULL,
	`hashId`	INTEGER NOT NULL,
	`data`	BLOB NOT NULL,
	PRIMARY KEY(id),
	FOREIGN KEY(`patchId`) REFERENCES Patches_id,
	FOREIGN KEY(`hashId`) REFERENCES Hashes_id
);
CREATE TABLE `Hashes` (
	`id`	INTEGER NOT NULL UNIQUE,
	`type`	INTEGER NOT NULL,
	`size`	INTEGER NOT NULL,
	`checksum`	BLOB NOT NULL,
	PRIMARY KEY(id),
	FOREIGN KEY(`type`) REFERENCES HashTypes_id
);
CREATE TABLE `HashTypes` (
	`id`	INTEGER NOT NULL UNIQUE,
	`name`	TEXT UNIQUE,
	PRIMARY KEY(id)
);
INSERT INTO `HashTypes` (id,name) VALUES (0,'Murmur128'),(1,'Md5');
COMMIT;";

        private static void InsertHash(SQLiteConnection conn, ref int hashId, FileValidation fv)
        {
            using (
                var finalHashCmd =
                    new SQLiteCommand("INSERT INTO Hashes(id, type, size, checksum) VALUES(@id, @type, @size, @checksum)", conn))
            {
                finalHashCmd.Parameters.Add(new SQLiteParameter("@id", ++hashId));
                finalHashCmd.Parameters.Add(new SQLiteParameter("@type", fv.Type + 1));
                finalHashCmd.Parameters.Add(new SQLiteParameter("@size", fv.Filesize));
                finalHashCmd.Parameters.Add(new SQLiteParameter("@checksum", fv.Checksum));

                Console.WriteLine($"Inserting: hash {hashId}");
                finalHashCmd.ExecuteNonQuery();
            }
        }

        internal static void Convert(string patPath)
        {
            foreach (var file in Directory.EnumerateFiles(patPath, "*.pat"))
            {
                var dbName = Path.GetFileName(Path.ChangeExtension(file, ".db3"));

                File.Delete(dbName);
                SQLiteConnection.CreateFile(dbName);
                using (var conn = new SQLiteConnection($"Data Source={dbName}"))
                {
                    conn.Open();
                    using (var schemaCmd = new SQLiteCommand(SchemaSetup, conn))
                    {
                        schemaCmd.ExecuteNonQuery();
                    }

                    Console.WriteLine($"Parsing {dbName}");
                    using (var fs = File.OpenRead(file))
                    using (var reader = new BinaryReader(fs))
                    {
                        var size = reader.ReadInt32();
                        using (var transaction = conn.BeginTransaction())
                        {
                            for (int i = 1, hashId = 0, patchId = 0, patchDataId = 0; i <= size; i++)
                            {
                                var key = reader.ReadString();
                                Console.WriteLine($"Inserting {key}");

                                var fv = FileValidation.ReadFrom(reader);

                                InsertHash(conn, ref hashId, fv);

                                using (
                                    var patchCmd =
                                        new SQLiteCommand(
                                            "INSERT INTO Patches(id, hashId, key) VALUES(@id, @hashId, @key)", conn))
                                {
                                    patchCmd.Parameters.Add(new SQLiteParameter("@id", ++patchId));
                                    patchCmd.Parameters.Add(new SQLiteParameter("@hashId", hashId));
                                    patchCmd.Parameters.Add(new SQLiteParameter("@key", key));

                                    Console.WriteLine($"Inserting patch {patchId}");
                                    patchCmd.ExecuteNonQuery();
                                }

                                var patchCount = reader.ReadInt32();
                                Console.WriteLine($"{patchCount} patches to insert");
                                for (var j = 0; j < patchCount; j++)
                                {
                                    var patch = new PatchInfo(reader);
                                    unsafe
                                    {
                                        fixed (byte* pData = patch.Data)
                                        {
                                            patch.Data = MakeDiff.ConvertPatch(pData, patch.Data.LongLength,
                                                Diff.SIG_LZDIFF41,
                                                Diff.SIG_NONONONO);
                                        }
                                    }
                                    InsertHash(conn, ref hashId, patch.Metadata);
                                    using (
                                        var blobCmd =
                                            new SQLiteCommand(
                                                "INSERT INTO PatchData(id, patchId, hashId, data) VALUES(@id, @patchId, @hashId, @data)",
                                                conn))
                                    {
                                        blobCmd.Parameters.Add(new SQLiteParameter("@id", ++patchDataId));
                                        blobCmd.Parameters.Add(new SQLiteParameter("@patchId", patchId));
                                        blobCmd.Parameters.Add(new SQLiteParameter("@hashId", hashId));
                                        blobCmd.Parameters.Add(new SQLiteParameter("@data", patch.Data));

                                        Console.WriteLine($"Inserting patchData {patchDataId}");
                                        blobCmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            transaction.Commit();
                        }
                    }
                }
            }
            Console.WriteLine("Done");
            Console.ReadKey();
        }
    }
}
