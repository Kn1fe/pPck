using System.IO;
using System;
using System.Text;
using System.Collections.Generic;
using ComponentAce.Compression.Libs.zlib;

namespace test_pck
{
    class fileTableEntry
    {
        public string filePath { get; set; }
        public string fullFilePath { get; set; }
        public uint fileDataOffset { get; set; }
        public int fileDataDecompressedSize { get; set; }
        public int fileDataCompressedSize { get; set; }
    }

    class Program
    {
        public static int KEY_1 = -1466731422;
        public static int KEY_2 = -240896429;
        public static int ASIG_1 = -33685778;
        public static int ASIG_2 = -267534609;
        public static int FSIG_1 = 1305093103;
        public static int FSIG_2 = 1453361591;

        public static int CompressionLevel = 9;

        static void Main(string[] args)
        {
            Console.WriteLine("by Kn1fe-Zone.Ru Team");
            if (args.Length > 1)
            {
                if (args.Length == 3)
                {
                    CompressionLevel = Convert.ToInt32(args[2]);
                }
                switch (args[0])
                {
                    case "-u":
                        unpack(args[1]);
                        break;
                    case "-p":
                        pack(args[1]);
                        break;
                }
            } else {
                Console.WriteLine("Bad agrs");
                Console.ReadKey();
            }
        }

        public static void unpack(string filepath)
        {
            string path = filepath + ".files\\";
            Merge(ref filepath);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.Delete(path, true);
            }
            BinaryReader br = new BinaryReader(new FileStream(filepath, FileMode.Open, FileAccess.Read));
            br.BaseStream.Seek(-8, SeekOrigin.End);
            int entryCount = br.ReadInt32();
            Console.WriteLine("Кол-во файлов: " + entryCount);
            br.BaseStream.Seek(-272, SeekOrigin.End);
            long fileTableOffset = (long)((ulong)((uint)(br.ReadUInt32() ^ (ulong)KEY_1)));
            Console.WriteLine("Адрес таблицы файлов: " + fileTableOffset);
            br.BaseStream.Seek(fileTableOffset, SeekOrigin.Begin);
            fileTableEntry[] table = new fileTableEntry[entryCount];
            for (int a = 0; a < entryCount; ++a)
            {
                int entrySize = br.ReadInt32() ^ KEY_1;
                entrySize = br.ReadInt32() ^ KEY_2;
                byte[] buffer = new byte[entrySize];
                buffer = br.ReadBytes(entrySize);
                if (entrySize < 276)
                {
                    table[a] = readTableEntry(buffer, true);
                }
                else
                {
                    table[a] = readTableEntry(buffer, false);
                }
                Console.Write(string.Format("\rЧтение файловой таблицы: {0}/{1}", a, entryCount));
            }
            Console.WriteLine();
            for (int b = 0; b < entryCount; ++b)
            {
                if (table[b].fileDataOffset <= 0)
                {
                    Console.WriteLine("\nПлохой адрес файла: " + table[b].fileDataOffset);
                }
                else
                {
                    CreateDir(path, table[b].filePath);
                    byte[] buffer = new byte[table[b].fileDataCompressedSize];
                    table[b].fullFilePath = path + table[b].filePath;
                    BinaryWriter bw = new BinaryWriter(new FileStream(table[b].fullFilePath, FileMode.Create, FileAccess.Write));
                    br.BaseStream.Seek(table[b].fileDataOffset, SeekOrigin.Begin);
                    buffer = br.ReadBytes(table[b].fileDataCompressedSize);
                    if (table[b].fileDataCompressedSize < table[b].fileDataDecompressedSize)
                    {
                        MemoryStream ms = new MemoryStream();
                        ZOutputStream zos = new ZOutputStream(ms);
                        CopyStream(new MemoryStream(buffer), zos, table[b].fileDataCompressedSize);
                        bw.Write(ms.ToArray());
                    }
                    else
                    {
                        bw.Write(buffer);
                    }
                    bw.Close();
                }
                Console.Write(string.Format("\rРаспаковка: {0}/{1}", b, entryCount));
            }
            br.Close();
            if (filepath.EndsWith("x")) File.Delete(filepath);
        }

        public static void pack(string dir)
        {
            string pck = dir.Replace(".files\\", "").Replace(".files", "") + "x";
            Console.WriteLine("Поиск файлов для запаковки");
            string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
            Console.WriteLine("Найдено файлов: " + files.Length);
            for (int a = 0; a < files.Length; ++a)
            {
                files[a] = files[a].Replace(dir, "").Replace("/", "\\").Remove(0, 1);
            }
            long fileTableOffset = 0;
            List<fileTableEntry> fileTable = new List<fileTableEntry>();
            BinaryWriter bw = new BinaryWriter(new FileStream(pck, FileMode.Create, FileAccess.ReadWrite));
            bw.Write(FSIG_1);
            bw.Write(0);
            bw.Write(FSIG_2);
            for (int a = 0; a < files.Length; ++a)
            {
                fileTableEntry fte = new fileTableEntry();
                MemoryStream fileMs = new MemoryStream(File.ReadAllBytes(dir + "\\" + files[a]));
                fte.filePath = files[a];
                fte.fileDataOffset = (uint)bw.BaseStream.Position;
                fte.fileDataDecompressedSize = (int)fileMs.Length;
                MemoryStream ms = new MemoryStream();
                CompressStream(fileMs, ms, (int)fileMs.Length);
                byte[] buffer = ms.ToArray();
                fte.fileDataCompressedSize = buffer.Length;
                bw.Write(buffer);
                fileTable.Add(fte);
                Console.Write(string.Format("\rЗаписываем файлы: {0}/{1}", a, files.Length));
            }
            fileTableOffset = bw.BaseStream.Position;
            Console.WriteLine();
            for (int a = 0; a < fileTable.Count; ++a)
            {
                byte[] buffer = writeTableEntry(fileTable[a]);
                bw.Write(buffer.Length ^ KEY_1);
                bw.Write(buffer.Length ^ KEY_2);
                bw.Write(buffer);
                Console.Write(string.Format("\rЗаписываем таблицу файлов: {0}/{1}", a, fileTable.Count));
            }
            bw.Write(ASIG_1);
            bw.Write((short)2);
            bw.Write((short)2);
            bw.Write((uint)(fileTableOffset ^ KEY_1));
            bw.Write(0);
            bw.Write(Encoding.Default.GetBytes("Angelica File Package, Perfect World."));
            byte[] nuller = new byte[215];
            bw.Write(nuller);
            bw.Write(ASIG_2);
            bw.Write(fileTable.Count);
            bw.Write((short)2);
            bw.Write((short)2);
            bw.BaseStream.Seek(4, SeekOrigin.Begin);
            bw.Write((uint)bw.BaseStream.Length);
            bw.Close();
            Split(pck);
        }

        public static fileTableEntry readTableEntry(byte[] buffer, bool compressed)
        {
            fileTableEntry fte = new fileTableEntry();
            MemoryStream ms = new MemoryStream(buffer);
            if (compressed)
            {
                byte[] buf = new byte[276];
                ZOutputStream zos = new ZOutputStream(new MemoryStream(buf));
                CopyStream(new MemoryStream(buffer), zos, 276);
                buffer = buf;
            }
            BinaryReader br = new BinaryReader(new MemoryStream(buffer));
            fte.filePath = Encoding.GetEncoding("GB2312").GetString(br.ReadBytes(260)).Split(new string[] { "\0" }, StringSplitOptions.RemoveEmptyEntries)[0].Replace("/", "\\");
            fte.fullFilePath = string.Empty;
            fte.fileDataOffset = br.ReadUInt32();
            fte.fileDataDecompressedSize = br.ReadInt32();
            fte.fileDataCompressedSize = br.ReadInt32();
            return fte;
        }

        public static byte[] writeTableEntry(fileTableEntry fte)
        {
            byte[] buffer = new byte[276];
            MemoryStream msb = new MemoryStream(buffer);
            BinaryWriter bw = new BinaryWriter(msb);
            bw.Write(Encoding.GetEncoding("GB2312").GetBytes(fte.filePath.Replace("/", "\\")));
            bw.BaseStream.Seek(260, SeekOrigin.Begin);
            bw.Write(fte.fileDataOffset);
            bw.Write(fte.fileDataDecompressedSize);
            bw.Write(fte.fileDataCompressedSize);
            bw.Write(0);
            msb.Seek(0, SeekOrigin.Begin);
            MemoryStream ms = new MemoryStream();
            CompressStream(msb, ms, 276);
            return ms.ToArray();
        }

        public static void CopyStream(Stream input, Stream output, int Size)
        {
            try
            {
                byte[] buffer = new byte[Size];
                int len;
                while ((len = input.Read(buffer, 0, Size)) > 0)
                {
                    output.Write(buffer, 0, len);
                }
                output.Flush();
            } catch {
                Console.WriteLine("\nBad zlib data");
            }
        }

        public static void CompressStream(Stream inStream, Stream outStream, int Size)
        {
            ZOutputStream zos = new ZOutputStream(outStream, CompressionLevel);
            CopyStream(inStream, zos, Size);
            zos.finish();
        }

        public static void CreateDir(string path, string subpath)
        {
            string[] subdirs = subpath.Split(new char[] { '\\' });
            for (int a = 0; a < subdirs.Length - 1; ++a)
            {
                path += "\\" + subdirs[a];
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
        }

        public static void Merge(ref string pck)
        {
            if (new FileInfo(pck).Length < 2147483393)
            {
                string pkx = pck.Replace(".pck", ".pkx");
                if (File.Exists(pkx))
                {
                    Console.WriteLine("\nСоединяем pck и pkx в новый файл");
                    if (File.Exists(pck + "x")) File.Delete(pck + "x");
                    File.Copy(pck, pck + "x");
                    CopyStream(new FileStream(pkx, FileMode.Open, FileAccess.Read), new FileStream(pck + "x", FileMode.Open, FileAccess.Write), 134217728);
                    pck = pck + "x";
                }
            }
        }

        public static void Split(string pck)
        {
            if (new FileInfo(pck).Length > 2147483393)
            {
                Console.WriteLine("\nРазъединяем pck на 2 архива");
                string pkx = pck.Replace(".pckx", ".pkx");
                FileStream fsPkx = new FileStream(pkx, FileMode.Create, FileAccess.Write);
                FileStream fsPckx = new FileStream(pck, FileMode.Open, FileAccess.ReadWrite);
                fsPckx.Seek(2147483392, SeekOrigin.Begin);
                CopyStream(fsPckx, fsPkx, 134217728);
                fsPckx.Seek(0, SeekOrigin.Begin);
                fsPckx.SetLength(2147483392);
                fsPkx.Close();
                fsPckx.Close();
            }
            if (File.Exists(pck.Remove(pck.Length - 1, 1)))
            {
                File.Delete(pck.Remove(pck.Length - 1, 1));
            }
            File.Move(pck, pck.Remove(pck.Length - 1, 1));
        }
    }
}
