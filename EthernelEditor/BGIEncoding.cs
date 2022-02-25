using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EthornellEditor
{
    public class BGIEncoding
    {
        public static Encoding BaseEncoding = Encoding.GetEncoding(932);
        public static string Decode(byte[] Data, out DataInfo DataInfo)
        {
            string Decoded = string.Empty;
            DataInfo = new DataInfo();
            List<byte> Buffer = new List<byte>();
            for (int i = 0; i < Data.Length; i++)
            {
                bool IsLastByte = i + 1 >= Data.Length;
                byte Byte = Data[i];

                if (Byte == 0xF5)
                {
                    int Reaming = Data.Length - i;
                    byte[] Sufix = new byte[Reaming];
                    for (int x = 0; x < Reaming; x++)
                        Sufix[x] = Data[i++];
                    DataInfo.Sufix = Sufix;
                    continue;
                }

                //Escape { and }
                if (Byte == 0x7B || Byte == 0x7D)
                    Buffer.Add(Byte);

                if ((Byte >= 0x81 && !IsLastByte && Data[i+1] < 0x40 && Data[i+1] > 0xAC) || Byte > 0x90 && !IsLastByte)
                {
                    Decoded += BaseEncoding.GetString(Buffer.ToArray());
                    Decoded += "{" + string.Format("{0:X2}{1:X2}", Byte, Data[++i]) + "}";
                    Buffer.Clear();
                    continue;
                }

                if (IsMultiByte(Byte) && !IsLastByte)
                {
                    Buffer.Add(Byte);
                    Buffer.Add(Data[++i]);
                    continue;
                }

                Buffer.Add(Byte);
            }

            if (Buffer.Count != 0)
                Decoded += BaseEncoding.GetString(Buffer.ToArray());

            if (Decoded.EndsWith("<"))
            {
                if (DataInfo.Sufix == null)
                    DataInfo.Sufix = new byte[0];
                DataInfo.Sufix = new byte[] { 0x3C }.Concat(DataInfo.Sufix).ToArray();
                Decoded = Decoded.Substring(0, Decoded.Length - 1);
            }

            return Decoded;
        }

        public static bool IsMultiByte(byte Byte) => (Byte > 0x80 && Byte < 0xA0) || ((Byte & 0xF0) == 0xE0);

        public static byte[] Encode(string Content, DataInfo DataInfo)
        {
            var Data = new List<byte>();
            var Buffer = string.Empty;
            for (int i = 0; i < Content.Length; i++)
            {
                char Char = Content[i];
                char? NChar = null;
                if (i + 1 < Content.Length)
                    NChar = Content[i + 1];

                if (Char == '{' && NChar != '{')
                {
                    Data.AddRange(BaseEncoding.GetBytes(Buffer));
                    Buffer = string.Empty;
                    i++;

                    var Hex = string.Empty;
                    while (Content[i] != '}')
                        Hex += Content[i++];

                    Hex = new string((from H in Hex where (H >= '0' && H <= '9') || (H >= 'A' && H <= 'F') || (H >= 'a' && H <= 'f') select H).ToArray());

                    for (int x = 0; x < Hex.Length / 2; x++)
                    {
                        Data.Add(byte.Parse(Hex.Substring(x * 2, 2), NumberStyles.HexNumber));
                    }
                    continue;
                }

                if (Char == '{' && NChar == '{')
                    i++;
                if (Char == '}' && NChar == '}')
                    i++;

                Buffer += Char;
            }

            if (Buffer != string.Empty)
                Data.AddRange(BaseEncoding.GetBytes(Buffer));


            if (DataInfo.Prefix == null)
                DataInfo.Prefix = new byte[0];
            if (DataInfo.Sufix == null)
                DataInfo.Sufix = new byte[0];

            return DataInfo.Prefix.Concat(Data).Concat(DataInfo.Sufix).Concat(new byte[] { 0x00 }).ToArray();
        }
    }

    public struct DataInfo
    {
        public byte[] Prefix;
        public byte[] Sufix;
    }
}
