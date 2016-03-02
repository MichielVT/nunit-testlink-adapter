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
        private TestProject m_testproject;

        private int m_buildid;
        private int m_projectid;
        private int m_testplanid;
        private int m_platformid;

        private TestLink proxy;
        private List<TestPlan> testPlans = new List<TestPlan>();
        private List<Build> builds = new List<Build>();
        private List<TestPlatform> platforms = new List<TestPlatform>();

        private Dictionary<string, int> testBuildDirectory = new Dictionary<string, int>();
        private Dictionary<string, int> testPlanDirectory = new Dictionary<string, int>();
        private Dictionary<string, int> testPlatformDirectory = new Dictionary<string, int>();
        private Dictionary<string, TestProject> testProjectDirectory = new Dictionary<string, TestProject>();

        public TestLinkProjectData(TestLink tl)
        {
            proxy = tl;

        }

        public void UpdateData(string project, string testplan, string platform, string build)
        {
            Project = project;
            Testplan = testplan;
            Build = build;
            Platform = platform;
        }

        private int UpdateDirectory<T>(IDictionary<string, int> directory, List<T> list, string key)
        {
            if (!directory.ContainsKey(key))
            {
                foreach (TL_Element e in list)
                {
                    if (e.Name == key)
                    {
                        directory.Add(key, e.Id);
                        return e.Id;
                    }
                }
            }
            return 0;
        }

        public String ProjectPrefix { get { return m_testproject.prefix; } }

        public String Project 
        { 
            get 
            { 
                return m_project; 
            } 
            private set 
            {
                try
                {
                    m_testproject = testProjectDirectory[value];
                }
                catch
                {
                    m_project = value;
                    m_testproject = proxy.GetProject(m_project);
                    testProjectDirectory[value] = m_testproject;
                }
                ProjectId = m_testproject.Id; 
                
            } 
        }
        
        public String Testplan 
        { 
            get 
            { 
                return m_testplan; 
            } 
            private set 
            { 
                m_testplan = value;

                try
                {
                    TestplanId = testPlanDirectory[value];
                }
                catch
                {
                    testPlans = proxy.GetProjectTestPlans(ProjectId);
                    TestplanId = UpdateDirectory(testPlanDirectory, testPlans, m_testplan);
                }
            } 
        }

        public String Platform 
        { 
            get 
            { 
                return m_platform; 
            } 
            private set 
            { 
                m_platform = value; 
                try
                {
                    PlatformId = testPlatformDirectory[value];
                }
                catch
                {
                    platforms = proxy.GetTestPlanPlatforms(TestplanId); 
                    PlatformId = UpdateDirectory(testPlatformDirectory, platforms, m_platform); 
                }
            } 
        }
        
        public String Build 
        { 
            get 
            { 
                return m_build; 
            }
            private set 
            { 
                m_build = value; 
                try
                {
                    BuildId = testBuildDirectory[value];
                }
                catch
                {
                    builds = proxy.GetBuildsForTestPlan(TestplanId);  
                    BuildId = UpdateDirectory(testBuildDirectory, builds, m_build); 
                }
            } 
        }
        
        public int BuildId { private set { m_buildid = value; } get { return m_buildid; } }
        public int ProjectId { private set { m_projectid = value; } get { return m_projectid; } }
        public int TestplanId { private set { m_testplanid = value; } get { return m_testplanid; } }
        public int PlatformId { private set { m_platformid = value; } get { return m_platformid; } }
    }
}
