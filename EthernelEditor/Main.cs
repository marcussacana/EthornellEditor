#define Filter
//#define OLDMETHOD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EthornellEditor {
    public class BurikoScript {
        public string[] strings = new string[0];
        public ScriptVersion Version { get; private set; }

        /// <summary>
        /// Alternative offset detection method, if enable, found more results in the script, if disable show only default string entry
        /// </summary>
        public bool CompatibilityMode = true;

        private StringEntry[] Strings = new StringEntry[0];
        private int StartTable = 0;
        private byte[] Script;
        private int HeaderSize = 0;

        private object[] HeaderMask = new object[]
        { 0x42, 0x75, 0x72, 0x69, 0x6B, 0x6F, 0x43, 0x6F, 0x6D, 0x70, 0x69, 0x6C, 0x65, 0x64, 0x53, 0x63, 0x72, 0x69, 0x70, 0x74, 0x56, 0x65, 0x72, 0x31, 0x2E, null, null, 0x00 };
        public string[] Import(byte[] Script) {
            this.Script = Script;
            strings = new string[0];
            Strings = new StringEntry[0];
            StartTable = Script.Length;
            while (!EqualAt(StartTable - 3, new byte[] { 0x00, 0x00, 0x00 }))
                StartTable--;
            HeaderSize = 0;
            if (EqualAt(0, HeaderMask)) {
                Version = ScriptVersion.WithSig;
                HeaderSize = HeaderMask.Length + Getoffset(HeaderMask.Length);
            } else {
                if (EqualAt(0, new byte[] { 0x42, 0x53, 0x45, 0x20, 0x31, 0x2E, 0x30 })) {
                    Version = ScriptVersion.BSE;
                    throw new Exception("Sorry this tool don't support the BSE encryption of the BGI");
                } else {
                    Version = ScriptVersion.Native;
                }
            }
#if OLDMETHOD
            bool finding = false;
            int Size = 0;
            for (int i = 0; i < StartTable; i++) {
                if (Size > 128) {
                    i -= Size;
                    i++;
                    finding = false;
                    Size = 0;
                }
                if (EqualAt(i, new byte[] { 0x7F, 0x00, 0x00, 0x00 }) && !finding) {
                    if (!CompatibilityMode) {
                        int offset = Getoffset(i + 4);
                        if (offset > 0 && offset < Script.Length)
                            StartTable = HeaderSize + offset;
                    }
                    Size = 0;
                    finding = true;
                    continue;
                }
                if (finding || !CompatibilityMode) {
                    if (EqualAt(i, new byte[] { 0x03, 0x00, 0x00, 0x00 })) {
                        extraString:;
                        i += 4;
                        int offset = Getoffset(i);
                        offset += HeaderSize;
                        if (offset > Script.Length || offset < StartTable) {
                            i -= 4;
                            Size++;
                            continue;
                        }
                        finding = false;
                        StringEntry rst = GetString(offset);
                        string str = rst.Content;
                        AppendArray(ref strings, str);
                        AppendArray(ref Strings, new StringEntry() {
                            Content = str,
                            OffsetPos = i
                        });
                        if (EqualAt(i + 4, new byte[] { 0x03, 0x00, 0x00, 0x00 }))//if have secondary STR
                        {
                            i += 4;
                            goto extraString;
                        }
                    } else
                        Size++;
                }
            }
#else
#if SAFE //Better Enabled but don't works with old games.
            for (int i = 0; i < StartTable; i+= 4) {
                if ((Script[i] == 0x03 || Script[i] == 0x01) && EqualAt(i + 1, new byte[] { 0x00, 0x00, 0x00 })) {
#else
            for (int i = 0; i < StartTable; i++) {
                if ((Script[i] == 0x03 || Script[i] == 0x01) && EqualAt(i + 1, new byte[] { 0x00, 0x00, 0x00 })) {
#endif
                    i += 4;
                    int Offset = Getoffset(i);
                    Offset += HeaderSize;
                    bool Ignore = false;
                    Ignore |= (from x in Strings where x.OffsetPos == i select x).Count() != 0;
                    Ignore |= Offset >= Script.Length;
                    Ignore |= Offset < StartTable;
                    
                    if (!Ignore)
                        Ignore |= Script[Offset - 1] != 0x00;
                    
                    if (Ignore) {
                        i -= 4;
                        continue;
                    }
                       
                    StringEntry Rst = GetString(Offset);
                    string Str = Rst.Content;

#if Filter
                    Ignore |= Str.EndsWith(".bs5") && Str[1] == ':';
                    if (Ignore) {
                        i -= 4;
                        continue;
                    }
#endif
                    AppendArray(ref strings, Str);
                    AppendArray(ref Strings, new StringEntry() {
                        Content = Str,
                        OffsetPos = i,
                        Info = Rst.Info
                    });
                }
            }
#endif
            return strings;
        }

        public void AppendArray<T>(ref T[] Array, T Value) {
            T[] Output = new T[Array.Length + 1];
            Array.CopyTo(Output, 0);
            Output[Array.Length] = Value;
            Array = Output;
        }

        //EditorSignature = 00 Edited With EthornellEditor 00 - This need to the program know if all strings is orderned at end of the script
        private byte[] EditorSignature = new byte[] { 0x00, 0x45, 0x64, 0x69, 0x74, 0x65, 0x64, 0x20, 0x57, 0x69, 0x74, 0x68, 0x20, 0x45, 0x74, 0x68, 0x6F, 0x72, 0x6E, 0x65, 0x6C, 0x6C, 0x45, 0x64, 0x69, 0x74, 0x6F, 0x72, 0x00 };

        public byte[] Export(string[] Strings) {
            strings = Strings;
            return Export();
        }

        public byte[] Export() {
            if (Script.Length == 0)
                throw new Exception("You need import before export.");
            bool haveSig = EndsWith(Script, EditorSignature);
            byte[] outfile = Script;
            //step 1 - Null all strings data
            if (!haveSig) {
                for (int pos = 0; pos < Strings.Length; pos++) {
                    int pointer = Getoffset(Strings[pos].OffsetPos) + HeaderSize;
                    while (outfile[pointer] != 0x00)
                        outfile[pointer++] = 0x00;
                }
            }

            //step 2 - Detect correct StringTable injection method
            int StringTableStart = 0;
            int DumpLen = 0;
            if (haveSig) {
                //step 3-1 - Detect Start Of StringTable
                for (int pos = Script.Length - EditorSignature.Length - 1; pos > 0; pos--)
                    if (EqualAt(pos, EditorSignature)) {
                        StringTableStart = pos;
                        break;
                    }
                if (StringTableStart == 0)
                    throw new Exception("Corrupted Script");

                DumpLen = StringTableStart + 1;//ajust cut pointer
            } else {
                //step 3-2 - Set the new Start of StringTable
                StringTableStart = Script.Length - 1;
                while (outfile[StringTableStart] == 0x00) {
                    StringTableStart--;
                }
                StringTableStart += 2;
                DumpLen = StringTableStart + 1;//ajust cut pointer
            }

            //step 4 - Generate new string table and calculate new offsets
            byte[] StringTable = new byte[0];
            int[] offsets = new int[Strings.Length];
            for (int pos = 0; pos < strings.Length; pos++) {
                int ID = -1;
                int Offset;

                for (int i = 0; i < pos; i++)
                    if (Strings[pos].Content == Strings[i].Content && EqualData(Strings[pos].Info.Prefix, Strings[i].Info.Prefix) && EqualData(Strings[pos].Info.Sufix, Strings[i].Info.Sufix)) {
                        ID = i;
                        break;
                    }
                

                if (ID == -1) {
                    Offset = (StringTableStart + EditorSignature.Length + StringTable.Length) - HeaderSize;
                    insertArr(ref StringTable, BGIEncoding.Encode(strings[pos].Replace("\\n", "\n"), Strings[pos].Info));
                } else {
                    Offset = offsets[ID];
                }

                offsets[pos] = Offset;
            }

            //step 5 - Generate new script
            outfile = cutFile(outfile, DumpLen);
            insertArr(ref outfile, EditorSignature);
            insertArr(ref outfile, StringTable);
            insertArr(ref outfile, EditorSignature);

            //step 6 - Update offsets
            for (int pos = 0; pos < offsets.Length; pos++) {
                int offsetPos = Strings[pos].OffsetPos;
                int offset = offsets[pos];
                overwriteBytes(ref outfile, genOffet(offset), offsetPos);
            }
            return outfile;
        }

        private byte[] cutFile(byte[] file, int position) {
            if (position >= file.Length)
                return file;
            else {
                byte[] result = new byte[position - 1];
                for (int pos = 0; pos < result.Length; pos++) {
                    result[pos] = file[pos];
                }
                return result;
            }
        }

        private bool EndsWith(byte[] Array, byte[] subArray) {

            for (int pos = subArray.Length; pos > 0; pos--) {
                byte a = Array[Array.Length - pos];
                byte b = subArray[subArray.Length - pos];
                if (a != b) {
                    return false;
                }
            }
            return true;
        }
        private void overwriteBytes(ref byte[] File, byte[] bytes, int Pos) {
            for (int pos = 0; pos < bytes.Length; pos++)
                File[Pos + pos] = bytes[pos];
        }

        private byte[] genOffet(int offset) {
            byte[] Off = BitConverter.GetBytes(offset);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(Off);
            return Off;

        }

        private void insertArr(ref byte[] original, byte[] ArryToAppend) {
            byte[] result = new byte[original.Length + ArryToAppend.Length];
            Array.Copy(original, 0, result, 0, original.Length);
            Array.Copy(ArryToAppend, 0, result, original.Length, ArryToAppend.Length);
            original = result;
        }

        private object[] haveStringAt(int pos, int[] offsets) {
            int id = 0;
            foreach (int SE in offsets) {
                int offset = SE;
                if (pos == offset)
                    return new object[] { true, offset, id };
                id++;
            }
            return new object[] { false, 0, 0 };
        }

        private int Getoffset(int pos) {
            byte[] off = new byte[4];
            off[0] = Script[pos]; off[1] = Script[pos + 1]; off[2] = Script[pos + 2]; off[3] = Script[pos + 3];
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(off);
            return BitConverter.ToInt32(off, 0);
        }

        private StringEntry GetString(int offset) {
            int ps = offset;
            while (Script[ps] != 0x00)
                ps++;
            int len = ps - offset;
            ps = 0;
            byte[] str = new byte[len];
            while (ps < len) {
                str[ps] = Script[offset + ps];
                ps++;
            }

            var Content = BGIEncoding.Decode(str, out DataInfo Info);
            return new StringEntry() {
                OffsetPos = offset,
                Content = Content,
                Info = Info
            };
        }

        private bool EqualAt(int At, byte[] Check) {
            if (Check.Length + At >= Script.Length)
                return false;
            for (int i = 0; i < Check.Length; i++)
                if (Check[i] != Script[i + At])
                    return false;
            return true;
        }
        private bool EqualData(byte[] Arr1, byte[] Arr2)
        {
            if (Arr1 == null && Arr2 == null)
                return true;

            if (Arr1 == null || Arr2 == null)
                return false;

            if (Arr1.Length != Arr2.Length)
                return false;

            for (int i = 0; i < Arr1.Length; i++)
                if (Arr1[i] != Arr2[i])
                    return false;

            return true;
        }
        private bool EqualAt(int At, object[] Check) {
            if (Check.Length + At >= Script.Length)
                return false;
            for (int i = 0; i < Check.Length; i++)
                if (Check[i] is byte || Check[i] is int)
                    if ((byte)(int)Check[i] != Script[i + At])
                        return false;
            return true;
        }
        public enum ScriptVersion {
            Native, WithSig,
            /// <summary>
            /// Don't supported, if you know how the encryption works, plz, contact-me in skype: live:ddtank.marcus
            /// </summary>
            BSE
        }
    }
    struct StringEntry {
        public int OffsetPos;
        public string Content;
        public DataInfo Info;
    }
}