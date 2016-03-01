using System;
using System.Collections.Generic;
using System.Text;

namespace Meyn.TestLink
{
    public class TestLinkConnectionData
    {
        private string m_url;
        private string m_user;
        private string m_devkey;

        public TestLinkConnectionData(string url, string devkey, string user)
        {
            Url = url;
            DevKey = devkey;
            User = user;
        }

        public TestLinkConnectionData()
        {
            Url = String.Empty;
            DevKey = String.Empty;
            User = String.Empty;
        }
        public String Url { get { return m_url; } private set { m_url = value; } }
        public String User { get { return m_user; } private set { m_user = value; } }
        public String DevKey { get { return m_devkey; } private set { m_devkey = value; } }

        public bool Equals(TestLinkConnectionData tlcd)
        {
            return ((tlcd.DevKey == DevKey) && (tlcd.Url == Url) && (tlcd.User == User));
        }
    }
}
