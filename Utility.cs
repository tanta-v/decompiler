﻿using System.IO;
using System.Text;
namespace decompiler
{
    public static class Utility
    {
        public static long UTCTimeAsLong { get { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); } }
        public static string timeNowAsString { get { return DateTime.Now.ToString(); } }
        public static void log(string text) => Console.WriteLine($"{timeNowAsString}: {text}");
        public static void log(int text) => log(text.ToString());
        public static void logRaw(string text) => Console.Write(text);
        public static uint toInt32fHex(string value) => Convert.ToUInt32(value, 16);
        public static int toInt32(byte[] value) => BitConverter.ToInt32(value);
        public static int toInt32(byte[] value, int offset) => BitConverter.ToInt32(value, offset);
        public static ushort toUInt16(byte[] value, int offset) => BitConverter.ToUInt16(value, offset);
        public static short toInt16(byte[] value, int offset) => BitConverter.ToInt16(value, offset);
        public static byte toHex(int value) => Convert.ToByte(value);
        public static string toHexStr(byte[] value) => string.Concat(value.Select(b => b.ToString("X2")));
        public static string getString(byte[] value) => Encoding.UTF8.GetString(value);
        public static string getStringAscii(byte[] value) => Encoding.UTF8.GetString(value);
        public static string getString(byte[] value, int offset, int count) => Encoding.UTF8.GetString(value, offset, count);
        public static string getStringAscii(byte[] value, int offset, int count) => Encoding.ASCII.GetString(value, offset, count);
        public static byte[] getBytesUTF8(string text) => Encoding.UTF8.GetBytes(text);
        
    }
}
