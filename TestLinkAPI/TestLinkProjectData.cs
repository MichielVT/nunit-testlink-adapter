using System;
using System.Collections.Generic;
using System.Text;

namespace Meyn.TestLink
{
    public class TestLinkProjectData
    {
        private string m_build;
        private string m_project;
        private string m_testplan;
        private string m_platform;

        public TestLinkProjectData(string build, string project, string testplan, string testsuite, string platform)
        {
            Build = build;
            Project = project;
            Testplan = testplan;
            Platform = platform;
        }

        public TestLinkProjectData()
        {
            Build = "";
            Project = "";
            Testplan = "";
            Platform = "";
        }

        public String Build { get { return m_build; } private set { m_build = value; } }
        public String Project { get { return m_project; } private set { m_project = value; } }
        public String Testplan { get { return m_testplan; } private set { m_testplan = value; } }
        public String Platform { get { return m_platform; } private set { m_platform = value; } }

    }
}
