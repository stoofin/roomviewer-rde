using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace viewer
{
    class BitStream:IDisposable
    {
        Stream stream;
        bool end;
        int bit;
        byte cbyte;
        bool write;

        public BitStream(Stream stream,bool write)
        {
            this.stream = stream;
            end = false;
            bit = cbyte = 0;
            this.write = write;
        }

        public bool End { get { return end; } }
        public int Bit { get { return bit; } }
        public byte Byte { get { return cbyte; } }
        public bool ToWrite { get { return write; } }

        public void Flush()
        {
            if (bit > 7)
            {
                stream.WriteByte(cbyte);
                cbyte = 0;
                bit = 0;
            }
            else
            {
                WriteBigEndian(0, (byte)(8 - bit));
            }
        }

        public Stream BaseStream
        {
            get { return stream; }
        }

        public void WriteBigEndian(int value, int bits)
        {
            int ecr = 0;

            if (bits > 0 && bits <= 32)
            {
                for (ecr = bits - 1; ecr >= 0; ecr--)
                {
                    if (bit > 7)
                    {
                        stream.WriteByte(cbyte);
                        cbyte = 0;
                        bit = 0;
                    }
                    cbyte = (byte)((cbyte << 1) + ((value >> ecr) & 1));
                    bit++;
                }
            }
        }

        public void WriteLittlEndian(int value, int bits)
        {
            byte c = 0;

            if (bits > 0 && bits <= 32)
            {
                while (bits >= 8 && !end)
                {
                    WriteBigEndian(((value >> (8 * c)) & 0xff), 8);
                    c++;
                    bits -= 8;
                }
                WriteBigEndian(value & 0xff, bits);
            }
        }

        public int ReadBigEndian(int bits)
        {
            int val;
            byte lus = 0;

            val = 0;

            if (bits > 0 && bits <= 32)
            {
                for (lus = 0; lus < bits && !end; lus++)
                {
                    bit--;
                    if (bit < 0)
                    {
                        int b = (byte)stream.ReadByte();
                        cbyte = (byte)b;
                        end = (b == -1);
                        bit = 7;
                    }
                    val = (val << 1) + ((cbyte >> bit) & 1);
                }
            }

            return (val);
        }

        public int ReadLittleEndian(int bits)
        {
            int val=0;
            byte c=0;

            if (bits > 0 && bits <= 32)
            {
                while (bits >= 8 && !end)
                {
                    val += ReadBigEndian((byte)8) << (8 * c);
                    c++;
                    bits -= 8;
                }
                val += ReadBigEndian(bits) << (8 * c);
            }

            return (val);
        }

        public void Close()
        {
            if (stream != null)
            {
                if (write)
                    Flush();
                stream.Close();
                stream = null;
                bit = 0;
                cbyte = 0;
                end = false;
            }
        }

        #region IDisposable ³ÉÔ±

        public void Dispose()
        {
            if (stream != null) stream.Close();
        }

        #endregion
    }

    class Lzss
    {
        public static readonly int LZSS_BUFFER_B = 12;
        public static readonly int LZSS_BUFFER = 4096;
        public static readonly int LZSS_CHAINE_B = 4;
        public static readonly int LZSS_CHAINE = 17;
        public static readonly int LZSS_LG_MIN = 2;
        public static readonly int LZSS_HEADER = 4;
        public static readonly int LZSS_UNKNOWN = 4;
        public static readonly int BYTE = 1;
        public static readonly int CODE = 0;

        private Lzss()
        {
        }
        public static void Decompress(BitStream lzss_f, Stream out_f, out int unknown)
        {

            int pos;
            int size;
            int type, length, chr;
            int position;
            int l;
            byte[] buffer = new byte[LZSS_BUFFER];

            uint t = (uint)lzss_f.ReadLittleEndian(8 * LZSS_HEADER); //sszl
            size = lzss_f.ReadLittleEndian(32);
            unknown = lzss_f.ReadBigEndian(8 * LZSS_UNKNOWN);
            pos = 0;
            while (pos < size)
            {
                type = lzss_f.ReadBigEndian(1);
                if (type == BYTE)
                {
                    length = 1;
                    chr = lzss_f.ReadBigEndian(8);
                    buffer[pos % LZSS_BUFFER] = (byte)chr;
                    out_f.WriteByte((byte)chr);
                }
                else
                {
                    position = (short)lzss_f.ReadBigEndian(LZSS_BUFFER_B) - 1;
                    position %= LZSS_BUFFER;
                    length = lzss_f.ReadBigEndian(LZSS_CHAINE_B) + LZSS_LG_MIN;
                    for (l = 0; l < length; l++)
                    {
                        buffer[(pos + l) % LZSS_BUFFER] = buffer[position];
                        out_f.WriteByte(buffer[position]);
                        position = (position + 1) % LZSS_BUFFER;
                    }
                }
                pos += length;
                out_f.Flush();
            }
        }
        public static void Compress(Stream in_f, BitStream lzss_out_f, int unknown)
        {
            int pos_f;
            int length;
            int position = 0, p, pos;
            int l;
            byte[] buffer = new byte[LZSS_BUFFER];

            lzss_out_f.WriteBigEndian(0x73737A6C, 32); //sszl
            lzss_out_f.WriteLittlEndian((int)in_f.Length, 32);

            lzss_out_f.WriteBigEndian(unknown, 8 * LZSS_UNKNOWN);

            int fl = (int)in_f.Length;

            in_f.Read(buffer, 0, 1);
            do
            {
                pos_f = (int)in_f.Position;
                pos = (pos_f - 1) % LZSS_BUFFER;
                length = 0;

                for (p = Math.Max(0, (pos_f - LZSS_BUFFER + LZSS_CHAINE));
                    p < pos_f - 1;
                    p++)
                {
                    l = 0;
                    while (l < LZSS_CHAINE && pos + l < LZSS_BUFFER && p % LZSS_BUFFER < LZSS_BUFFER - 2
                        && buffer[(p + l) % LZSS_BUFFER] == buffer[(pos + l) % LZSS_BUFFER] && in_f.Position <= fl)
                    {
                        l++;
                        if (l > length)
                            buffer[(pos + l) % LZSS_BUFFER] = (byte)in_f.ReadByte();
                        //if (feof(in_f)!=0)
                        //bit_ftell(lzss_out_f);
                    }
                    if (l >= length)
                    {
                        length = l;
                        position = p % LZSS_BUFFER;
                    }
                }
                if (length >= LZSS_LG_MIN)
                {
                    lzss_out_f.WriteBigEndian(CODE, 1);
                    lzss_out_f.WriteBigEndian(position + 1, 12);
                    lzss_out_f.WriteBigEndian(length - LZSS_LG_MIN, 4);
                }
                else
                {
                    if (length == 0)
                        in_f.Read(buffer, pos + 1, 1);
                    length = 1;
                    lzss_out_f.WriteBigEndian(BYTE, 1);
                    lzss_out_f.WriteBigEndian(buffer[pos], 8);

                }
            } while (in_f.Position < fl);
            lzss_out_f.Flush();
            lzss_out_f.WriteBigEndian(0x0000, 16);
            while (lzss_out_f.BaseStream.Position % 8 != 5)
                lzss_out_f.WriteBigEndian(0x00, 8);
            lzss_out_f.WriteBigEndian(0x7777, 16);
        }
        
    }
}
