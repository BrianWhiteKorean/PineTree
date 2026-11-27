using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PineTree.Classes
{
    /* ini 파일 사용하기 *********************************/
    public class iniUtil
    {

        private string iniPath;

        public iniUtil(string path)
        {
            this.iniPath = path;  //INI 파일 위치를 생성할때 인자로 넘겨 받음
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(
            String section,
            String key,
            String def,
            StringBuilder retVal,
            int size,
            String filePath);



        [DllImport("kernel32.dll")]
        private static extern long WritePrivateProfileString(
             String section,
             String key,
             String val,
             String filePath);


        // INI 값을 읽어 온다. 
        public String GetIniValue(String Section, String Key)
        {
            StringBuilder temp = new StringBuilder(255);
            int i = GetPrivateProfileString(Section, Key, "", temp, 255, iniPath);
            return temp.ToString();
        }

        public int GetIniValueInt(String Section, String Key)
        {
            StringBuilder temp = new StringBuilder(255);
            int i = GetPrivateProfileString(Section, Key, "", temp, 255, iniPath);

            int nValue = 0;
            try
            {
                nValue = Convert.ToInt32(temp);
            }
            catch (Exception ex)
            {
            }
            return nValue;
        }

        // INI 값을 셋팅
        public void SetIniValue(String Section, String Key, String Value)
        {
            WritePrivateProfileString(Section, Key, Value, iniPath);
        }
    }
}
