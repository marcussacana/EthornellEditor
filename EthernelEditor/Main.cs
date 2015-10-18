using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EthornellEditor
{
    public class BurikoScript
    {
        public string[] strings = new string[0];
        private StringEntry[] Strings = new StringEntry[0];
        private int StartTable = 0;
        private byte[] script;
        public void import(byte[] Script)
        {
            script = Script;
            strings = new string[0];
            Strings = new StringEntry[0];
            StartTable = Script.Length;
            bool finding = false;
            int Size = 0;
            for (int pointer = 0; pointer < StartTable; pointer++)
            {
                if (Size > 128)
                {
                    pointer -= Size;
                    pointer++;
                    finding = false;
                    Size = 0;
                }
                if (EqualAt(pointer, new byte[] { 0x7F, 0x00, 0x00, 0x00 }, Script) && !finding)
                {
                    pointer += 4;
                    if (StartTable == Script.Length)
                        StartTable = Tools.ByteArrayToInt(new byte[] { Script[pointer + 3], Script[pointer + 2], Script[pointer + 1], Script[pointer] });
                    Size = 0;
                    finding = true;
                    continue;
                }
                if (finding)
                {
                    if (EqualAt(pointer, new byte[] { 0x03, 0x00, 0x00, 0x00 }, Script))
                    {
                    extraString:;
                        pointer += 4;
                        int offset = Tools.ByteArrayToInt(new byte[] { Script[pointer + 3], Script[pointer + 2], Script[pointer + 1], Script[pointer] });
                        if (offset > Script.Length || offset < StartTable)
                        {
                            pointer -= 4;
                            Size++;
                            continue;
                        }
                        finding = false;                        
                        object[] rst = getString(offset, Script);
                        string str = (string)rst[0];
                        int length = (int)rst[1];
                        object[] temp = new string[strings.Length+1];
                        strings.CopyTo(temp, 0);
                        temp[strings.Length] = str;
                        strings = (string[])temp;
                        temp = new StringEntry[Strings.Length+1];
                        Strings.CopyTo(temp, 0);
                        StringEntry SE = new StringEntry();
                        SE.content = str;
                        SE.OffsetPos = pointer;
                        SE.Size = length;
                        temp[Strings.Length] = SE;
                        Strings = (StringEntry[])temp;
                        if (EqualAt(pointer+4, new byte[] { 0x03, 0x00, 0x00, 0x00 }, Script))//if have secondary STR
                        {
                            pointer += 4;
                            goto extraString;
                        }
                    }
                }
                else
                    Size++;
            }
        }
        //EditorSignature = 00 Edited With EthornellEditor 00 - This need to the program know if all strings is orderned at end of the script
        private byte[] EditorSignature = new byte[] { 0x00, 0x45, 0x64, 0x69, 0x74, 0x65, 0x64, 0x20, 0x57, 0x69, 0x74, 0x68, 0x20, 0x45, 0x74, 0x68, 0x6F, 0x72, 0x6E, 0x65, 0x6C, 0x6C, 0x45, 0x64, 0x69, 0x74, 0x6F, 0x72, 0x00 };
        public byte[] export() //maked with a prevent of strings without call order
        {
            bool haveSig = EndsWith(script, EditorSignature);
            byte[] outfile = script;
            //step 1 - Null all strings data
            if (!haveSig)
            {
                for (int pos = 0; pos < Strings.Length; pos++)
                {
                    int pointer = getoffset(Strings[pos].OffsetPos);
                    while (outfile[pointer] != 0x00)
                    {
                        outfile[pointer] = 0x00;
                        pointer++;
                    }
                }
            }
            //step 2 - Detect correct StringTable injection method

            int TableStart = 0;
            if (haveSig)
            {
                //step 3.1 - Detect Start Of StringTable
                for (int pos = script.Length - EditorSignature.Length; pos > 0; pos--)
                {
                    if (EqualAt(pos, EditorSignature, script))
                    {
                        TableStart = pos;
                        break;
                    }
                }
                if (TableStart == 0)
                {
                    throw new Exception("Corrupted Script");
                }
                while (script[TableStart] == 0x00)
                {
                    TableStart--;
                }
                TableStart += 2; //ajust cut pointer
            }
            else
            {
                //step 3.2 - Set the new Start of StringTable
                TableStart = script.Length - 1;
                while (outfile[TableStart] == 0x00)
                {
                    TableStart--;
                }
                TableStart += 2; //ajust cut pointer
            }
            //step 4 - Generate new string table
            byte[] StringTable = new byte[0];
            object[] offsets = new object[] { new int[0], new int[0] };
            for (int pos = 0; pos < strings.Length; pos++)
            {
                int Offset = (TableStart + EditorSignature.Length - 1) + StringTable.Length;  
                int OffsetPos = Strings[pos].OffsetPos;
                string[] hexs = Tools.SJStringToHex(strings[pos].Replace("\\n", "\n"));
                string hex = "";
                for (int i = 0; i < hexs.Length; i++)
                {
                    hex += hexs[i] + " ";
                }
                hex = hex.Substring(0, hex.Length-1);
                byte[] newstring = Tools.StringToByteArray(hex);
                newstring = insertArr(newstring, new byte[] { 0x00 });
                StringTable = insertArr(StringTable, newstring);
                int[] OffPos = (int[])offsets[0];
                int[] Offsets = (int[])offsets[1];
                int[] tmp = new int[OffPos.Length + 1];
                OffPos.CopyTo(tmp, 0);
                tmp[OffPos.Length] = OffsetPos;
                OffPos = tmp;
                tmp = new int[Offsets.Length+1];
                Offsets.CopyTo(tmp, 0);
                tmp[Offsets.Length] = Offset;
                Offsets = tmp;
                offsets = new object[] { OffPos, Offsets };                
            }
            
            //step 5 - Generate new script
            outfile = cutFile(outfile, TableStart);
            StringTable = insertArr(EditorSignature, StringTable);
            StringTable = insertArr(StringTable, EditorSignature);
            byte[] temp = new byte[outfile.Length + StringTable.Length];
            outfile.CopyTo(temp, 0);
            StringTable.CopyTo(temp, outfile.Length);
            outfile = temp;
            //step 6 - Update offsets
            for(int pos = 0; pos <  ((int[])offsets[0]).Length; pos++)
            {
                int offsetPos = ((int[])offsets[0])[pos];
                int offset = ((int[])offsets[1])[pos];
                outfile = writeOffset(outfile, genOffet(offset), offsetPos);
            }
            return outfile;
        }

        private byte[] cutFile(byte[] file, int position)
        {
            if (position >= file.Length)
                return file;
            else
            {
                byte[] result = new byte[position-1];
                for (int pos = 0; pos < result.Length; pos++)
                {
                    result[pos] = file[pos];
                }
                return result;
            }
        }

        private bool EndsWith(byte[] Array, byte[] subArray)
        {

            for (int pos = subArray.Length; pos > 0; pos--)
            {
                byte a = Array[Array.Length - pos];
                byte b = subArray[subArray.Length - pos];
                if (a != b)
                {
                    return false;
                }
            }
            return true;
        }
        private byte[] writeOffset(byte[] File, byte[] offset, int offsetPos)
        {
            for (int pos = 0; pos < offset.Length; pos++)
            {
                File[offsetPos + pos] = offset[pos];
            }
            return File;
        }

        private byte[] genOffet(int offset)
        {
            byte[] result = new byte[4];
            string hex = Tools.IntToHex(offset);
            if (hex.Length % 2 != 0)
            {
                hex = "0" + hex;
            }
            byte[] off = Tools.StringToByteArray(hex);
            switch (off.Length)
            {
                case 1:
                    result = new byte[] { off[0], 0x00, 0x00, 0x00 };
                    break;
                case 2:
                    result = new byte[] { off[1], off[0], 0x00, 0x00 };
                    break;
                case 3:
                    result = new byte[] { off[2], off[1], off[0], 0x00 };
                    break;
                case 4:
                    result = new byte[] { off[3], off[2], off[1], off[0] };
                    break;
            }
            return result;

        }

        private byte[] insertArr(byte[] original, byte[] ArryToAppend)
        {
            byte[] result = original;
            foreach (byte bt in ArryToAppend)
            {
                byte[] temp = new byte[result.Length+1];
                result.CopyTo(temp, 0);
                temp[result.Length] = bt;
                result = temp;
            }
            return result;
        }

        private object[] haveStringAt(int pos, int[] offsets)
        {
            int id = 0;
            foreach (int SE in offsets)
            {
                int offset = SE;
                if (pos == offset)
                    return new object[] { true, offset, id };
                id++;
            }
            return new object[] { false, 0, 0 };
        }

        private int getoffset(int pos) {
            return Tools.ByteArrayToInt(new byte[] { script[pos+3], script[pos+2], script[pos+1], script[pos] });
        }

        private object[] getString(int offset, byte[] script)
        {
            byte[] str = new byte[0];
            while (script[offset] != 0x00)
            {
                byte[] temp = new byte[str.Length + 1];
                str.CopyTo(temp, 0);
                temp[str.Length] = script[offset];
                str = temp;
                offset++;
            }
            string[] hex = Tools.ByteArrayToString(str).Split('-');
            return new object[] { Tools.SJHexToString(hex).Replace("\n", "\\n"), hex.Length };
        }

        private bool EqualAt(int offset, byte[] check, byte[] script)
        {
            for (int index = 0; index < check.Length; index++)
                if (check[index] != script[index + offset])
                    return false;
            return true;
        }
    }
    class StringEntry {
        public int OffsetPos = 0;
        public int Size = 0;
        public string content = "";
    }
    class Tools
    {
        public static string IntToHex(int val)
        {
            return val.ToString("X");
        }
        public static int ByteArrayToInt(byte[] array)
        {
            return Tools.HexToInt(Tools.ByteArrayToString(array).Replace(@"-", ""));
        }
        public static string StringToHex(string _in)
        {
            string input = _in;
            char[] values = input.ToCharArray();
            string r = "";
            foreach (char letter in values)
            {
                int value = Convert.ToInt32(letter);
                string hexOutput = System.String.Format("{0:X}", value);
                if (value > 255)
                    return UnicodeStringToHex(input);
                r += value + " ";
            }
            string[] bytes = r.Split(' ');
            byte[] b = new byte[bytes.Length - 1];
            int index = 0;
            foreach (string val in bytes)
            {
                if (index == bytes.Length - 1)
                    break;
                if (int.Parse(val) > byte.MaxValue)
                {
                    b[index] = byte.Parse("0");
                }
                else
                    b[index] = byte.Parse(val);
                index++;
            }
            r = ByteArrayToString(b);
            return r.Replace("-", @" ");
        }
        public static string UnicodeStringToHex(string _in)
        {
            string input = _in;
            char[] values = Encoding.Unicode.GetChars(Encoding.Unicode.GetBytes(input.ToCharArray()));
            string r = "";
            foreach (char letter in values)
            {
                int value = Convert.ToInt32(letter);
                string hexOutput = System.String.Format("{0:X}", value);
                r += value + " ";
            }
            UnicodeEncoding unicode = new UnicodeEncoding();
            byte[] b = unicode.GetBytes(input);
            r = ByteArrayToString(b);
            return r.Replace("-", @" ");

        }
        public static string U8HexToString(string[] hex)
        {
            byte[] str = StringToByteArray(hex);
            UTF8Encoding encoder = new UTF8Encoding();
            return encoder.GetString(str);
        }
        public static string[] U8StringToHex(string text)
        {
            UTF8Encoding encoder = new UTF8Encoding();
            byte[] cnt = encoder.GetBytes(text.ToCharArray());
            return ByteArrayToString(cnt).Split('-');
        }

        public static string SJHexToString(string[] hex)
        {
            byte[] str = StringToByteArray(hex);
            Encoding encoder = Encoding.GetEncoding(932);
            return encoder.GetString(str);
        }
        public static string[] SJStringToHex(string text)
        {
            Encoding encoder = Encoding.GetEncoding(932);
            byte[] cnt = encoder.GetBytes(text.ToCharArray());
            return ByteArrayToString(cnt).Split('-');
        }
        public static byte[] StringToByteArray(string hex)
        {
            try
            {
                hex = hex.Replace(@" ", "");
                int NumberChars = hex.Length;
                byte[] bytes = new byte[NumberChars / 2];
                for (int i = 0; i < NumberChars; i += 2)
                    bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                return bytes;
            }
            catch { Console.Write("Invalid format file!"); return new byte[0]; }
        }
        public static byte[] StringToByteArray(string[] hex)
        {
            try
            {
                int NumberChars = hex.Length;
                byte[] bytes = new byte[NumberChars];
                for (int i = 0; i < NumberChars; i++)
                    bytes[i] = Convert.ToByte(hex[i], 16);
                return bytes;
            }
            catch { Console.Write("Invalid format file!"); return new byte[0]; }
        }
        public static string ByteArrayToString(byte[] ba)
        {
            string hex = BitConverter.ToString(ba);
            return hex;
        }

        public static int HexToInt(string hex)
        {
            int num = Int32.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            return num;
        }

        public static string HexToString(string hex)
        {
            string[] hexValuesSplit = hex.Split(' ');
            string returnvar = "";
            foreach (string hexs in hexValuesSplit)
            {
                int value = Convert.ToInt32(hexs, 16);
                char charValue = (char)value;
                returnvar += charValue;
            }
            return returnvar;
        }

        public static string UnicodeHexToUnicodeString(string hex)
        {
            string hexString = hex.Replace(@" ", "");
            int length = hexString.Length;
            byte[] bytes = new byte[length / 2];

            for (int i = 0; i < length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            }

            return Encoding.Unicode.GetString(bytes);
        }

    }
}
