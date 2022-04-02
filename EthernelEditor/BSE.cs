using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EthornellEditor
{
    class BSE
    {
		//Without samples to test it
		//Stolen From: https://github.com/minirop/arc-reader/blob/master/bse.c
		public static bool bse_decrypt(byte[] crypted)
		{
			int x = 10;

			int hash = 0;
			byte sum_check = 0;
			byte xor_check = 0;
			byte sum_data = 0;
			byte xor_data = 0;
			int[] flags = new int[64];
			int counter = 0;


			sum_check = crypted[x++];
			xor_check = crypted[x++];
			hash = BitConverter.ToInt32(crypted, x);

			x += 4;

			for (counter = 0; counter < 64; counter++)
			{
				int target = 0;
				int s, k;
				int r = bse_rand(ref hash);
				int i = r & 0x3F;

				while (flags[i] != 0)
				{
					i = (i + 1) & 0x3F;
				}

				r = bse_rand(ref hash);
				s = r & 0x07;
				target = i;

				k = bse_rand(ref hash);
				r = bse_rand(ref hash);
				r = ((crypted[target + x] & 255) - r) & 255;

				if ((k & 1) != 0)
				{
					crypted[target + x] = (byte)(r << s | r >> (8 - s));
				}
				else
				{
					crypted[target + x] = (byte)(r >> s | r << (8 - s));
				}

				flags[i] = 1;
			}

			for (counter = 0; counter < 64; counter++)
			{
				sum_data = (byte)(sum_data + (crypted[counter + x] & 255));
				xor_data = (byte)(xor_data ^ (crypted[counter + x] & 255));
			}

			if (sum_data == sum_check && xor_data == xor_check)
			{
				return true;
			}

			return false;
		}

		static int bse_rand(ref int seed)
		{
			int tmp = (((seed * 257 >> 8) + seed * 97) + 23) ^ -1496474763;
			seed = ((tmp >> 16) & 65535) | (tmp << 16);
			return seed & 32767;
		}
	}
}
