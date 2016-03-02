/* 
TestLink API library
Copyright (c) 2009, Stephan Meyn <stephanmeyn@gmail.com>

Permission is hereby granted, free of charge, to any person 
obtaining a copy of this software and associated documentation 
files (the "Software"), to deal in the Software without restriction, 
including without limitation the rights to use, copy, modify, merge, 
publish, distribute, sublicense, and/or sell copies of the Software, 
and to permit persons to whom the Software is furnished to do so, 
subject to the following conditions:

The above copyright notice and this permission notice shall be 
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES 
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
DEALINGS IN THE SOFTWARE.
*/


using System;
using System.Collections.Generic;
using log4net;

namespace Meyn.TestLink
{
    /// <summary>
    /// encapsulates the basic comms to the TestLink API 
    /// for the purpose of recording test results based upon the TestLinkFixtureAttribute 
    /// information.
    /// </summary>
    /// <remarks>This class is used by test exporters for automated testing frameworks such as Gallio or NUnit</remarks>
    public class TestLinkAdaptor
    {
        enum ProjectData
        { 
            TestPlan,
            TestSuite,
            Build,
            Platform,
            Project
        }

        private readonly ILog log;


        public TestLinkAdaptor(ILog logger)
        {
            log = logger;
        }

        private TestLinkProjectData projectData;

        private TestLinkConnectionData connectionData;

        //private bool connectionValid = false;
        /// <summary>
        /// can we talk to testlink
        /// </summary>
        private bool basicConnectionValid = false;
        /// <summary>
        /// do we have project plan, project id and test suite correct?
        /// </summary>
        private bool projectDataValid = false;

        private Dictionary<string, int> testSuiteDirectory = new Dictionary<string, int>();

        /// <summary>
        /// can we record test results
        /// </summary>
        public bool ConnectionValid
        {
            get { return (basicConnectionValid && projectDataValid); }
        }
       
        private TestLinkException lastException = null;

        public TestLinkException LastException
        {
            get { return lastException; }
        }
        /// <summary>
        /// the proxy to the current server
        /// </summary>
        Meyn.TestLink.TestLink proxy = null;

        #region recording the result
        /// <summary>
        /// record a result with testlink;
        /// </summary>
        /// <returns></returns>
        public GeneralResult RecordTheResult(string testName, string testSuite, TestCaseResultStatus status, string notes)
        {
            GeneralResult result = null;
            if (ConnectionValid == true)
            {
                int testCaseId = AddTestIfNotExisting(testName, testSuite);
                if (testCaseId == 0)
                {
                    result = new GeneralResult("Unable to find/add testcase", false);
                }
                else
                {
                    result = proxy.ReportTCResult(testCaseId, projectData.TestplanId, status, platformName: projectData.Platform, notes: notes.ToString(), buildid: projectData.BuildId);
                }
            }
            else
            {
                result = new GeneralResult("Invalid Connection", false);
            }
            return result;
        }

        private int GetPlatformId(int testPlanId, string platformName)
        {
            List<TestPlatform> platforms = proxy.GetTestPlanPlatforms(testPlanId);

            foreach (TestPlatform tp in platforms)
            {
                if (tp.name == platformName)
                {
                    return tp.id;
                }
            }
            return 0;
        }

        public int AddTestIfNotExisting(string testName, string testSuite, string testDescription = "")
        {
            int testSuiteId;

            if (testSuiteDirectory.ContainsKey(testSuite))
            {
                testSuiteId = testSuiteDirectory[testSuite];
            }
            else
            {
                testSuiteId = GetTestSuiteId(projectData.ProjectId, testSuite, true);
                testSuiteDirectory.Add(testSuite, testSuiteId);
            }
            
            int TCaseId = getTestCaseByName(testName, testSuiteId);
            GeneralResult result;

            if (TCaseId == 0)
            {
                // need to create test case
                result = proxy.CreateTestCase(connectionData.User, testSuiteId, testName, projectData.ProjectId,
                    testDescription, new TestStep[0], "", 0,
                    true, ActionOnDuplicatedName.Block, 2, 2);
                TCaseId = result.additionalInfo.id;
                int tcExternalId = result.additionalInfo.external_id;
                if (result.status == false)
                {
                    Console.Error.WriteLine("Failed to create TestCase for {0}", testName);
                    Console.Error.WriteLine(" Reason {0}", result.message);
                    return 0;
                }
                string externalId = string.Format("{0}-{1}", projectData.ProjectPrefix, tcExternalId);
                int featureId = proxy.addTestCaseToTestPlan(projectData.ProjectId, projectData.TestplanId, externalId, result.additionalInfo.version_number, projectData.PlatformId);
                if (featureId == 0)
                {
                    Console.Error.WriteLine("Failed to assign TestCase {0} to testplan", testName);
                    return 0;
                }
            }

            return TCaseId;
        }

        /// <summary>
        /// get the test case by this name in this particular test suite
        /// </summary>
        /// <param name="testCaseName"></param>
        /// <param name="testSuiteId">the test suite the test case has to be in</param>
        /// <returns>a valid test case id or 0 if no test case was found</returns>
        private int getTestCaseByName(string testCaseName, int testSuiteId)
        {
            List<TestCaseId> idList = proxy.GetTestCaseIDByName(testCaseName);
            if (idList.Count == 0)
                return 0;
            foreach (TestCaseId tc in idList)
                if (tc.parent_id == testSuiteId)
                    return tc.id;
            return 0;
        }
        #endregion

        #region updates after changes in the TestLinkFixture Data
        /// <summary>
        /// try to update the connection with a minimum of API calls to testlink
        /// </summary>
        /// <param name="newData"></param>
        public bool UpdateConnectionData(TestLinkConnectionData newData)
        {
            if (newData == null)
            {
                log.Error("No TestLinkFixture detected");
                basicConnectionValid = false;
                connectionData = null;
                return false;
            }

            if (connectionData == null)
            {
                connectionData = new TestLinkConnectionData();
            }

            //attempt a new connection if url or devkey are different
            if (!connectionData.Equals(newData))
            {
                basicConnectionValid = basicConnection(newData);
            }
            connectionData = newData;
            return basicConnectionValid;
        }
        /// <summary>
        /// create the basic connection and test it out
        /// </summary>
        private bool basicConnection(TestLinkConnectionData connection)
        {
            lastException = null;
            proxy = new TestLink(connection.DevKey, connection.Url);
            try
            {
                proxy.SayHello();
            }
            catch (TestLinkException tlex)
            {
                lastException = tlex;
                log.ErrorFormat("Failed to connect to TestLink at {1}. Message was '{0}'", tlex.Message, connection.Url);
                return false;
            }
            return true;
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

        public bool UpdateProjectData(string projectName, string testplanName, string platformName, string buildName)
        {
            if (projectData == null)
            {
                projectData = new TestLinkProjectData(proxy);
            }
            try
            {
                projectData.UpdateData(projectName, testplanName, platformName, buildName);
            }
            catch (Exception ex)
            {
                log.Error("Error updating project data: " + ex.Message);
                projectDataValid = false;
                return false;
            }
            projectDataValid = true;
            return true;
        }
   
        
        /// <summary>
        /// retrieve the testsuite id 
        /// </summary>
        /// <returns>0 or a valid test suite Id</returns>
        private int GetTestSuiteId(int projectId, string testSuiteName, bool createIfNotExisting)
        {
            int testSuiteId = 0;
            int parentTestSuiteId = 0;
            GeneralResult result;
            string[] suites = testSuiteName.Split(new char[] { '.' });
            List<Meyn.TestLink.TestSuite> testSuites = proxy.GetFirstLevelTestSuitesForTestProject(projectId); //GetTestSuitesForTestPlan(testPlanId);

            for (int i = 0; i < suites.Length; i++)
            {
                testSuiteId = 0;
                foreach (Meyn.TestLink.TestSuite ts in testSuites)
                {
                    if (ts.name == suites[i])
                    {
                        parentTestSuiteId = testSuiteId = ts.id;
                        break;
                    }
                }
                if (testSuiteId == 0)
                {
                    if (createIfNotExisting)
                    {
                        result = proxy.CreateTestSuite(projectId, suites[i], "", parentTestSuiteId);
                        if (result.status == false)
                        {
                            log.ErrorFormat("Error trying to create new testsuite {0} ({1}): {2}", suites[i], testSuiteName, result.message);
                            return 0;
                        }
                        log.InfoFormat("Created new testsuite {0} ({1})", suites[i], testSuiteName);
                        parentTestSuiteId = testSuiteId = result.id;
                    }
                    else
                    {
                        return 0;
                    }
                }
                testSuites = proxy.GetTestSuitesForTestSuite(testSuiteId);
            }
            return testSuiteId;
        }

        #endregion
    }
}
