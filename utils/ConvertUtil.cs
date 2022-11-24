using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Opc.Cpx;

namespace OpcDAToMSA.utils
{
    class ConvertUtil
    {

        public byte[] StringToBytes(string s)
        {
            int sLen = s.Length;
            int bytesLen = sLen / 2;
            byte[] bytes = new byte[bytesLen];
            int position = 0;
            for (int i = 0; i < bytesLen; i++)
            {
                string abyte = s.Substring(position, 2);
                bytes[i] = Convert.ToByte(abyte, 16);
                position += 2;
            }
            return bytes;
        }


        public static byte[] ObjectToBytes(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                IFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                return ms.GetBuffer();
            }
        }

        /// <summary> 
        /// 将一个序列化后的byte[]数组还原         
        /// </summary>
        /// <param name="Bytes"></param>         
        /// <returns></returns> 
        public static object BytesToObject(byte[] Bytes)
        {
            using (MemoryStream ms = new MemoryStream(Bytes))
            {
                IFormatter formatter = new BinaryFormatter();
                return formatter.Deserialize(ms);
            }
        }

        public static byte[] BoolToBytes(bool value)
        {
            byte[] src = new byte[2];
            if (value == true)
            {
                src[1] = 0x01;
            }
            else
            {
                src[1] = 0x00;
            }
            src[0] = 0x00;
            return src;
        }
    }
}
