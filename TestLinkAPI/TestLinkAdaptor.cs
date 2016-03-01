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
        List<TestProject> allProjects = new List<TestProject>();
        List<TestPlan> AllTestPlans = new List<TestPlan>();
        private TestProject currentProject;

        private int testSuiteId;
        private int testBuildId;
        private int testPlanId;
        private int testProjectId;
        private int platformId;

        #region recording the result
        /// <summary>
        /// record a result with testlink;
        /// </summary>
        /// <param name="testCaseId"></param>
        /// <param name="status"></param>
        /// <param name="notes"></param>
        /// <returns></returns>
        public GeneralResult RecordTheResult(int testCaseId, TestCaseResultStatus status, string notes)
        {
            GeneralResult result = null;
            if (ConnectionValid == true)
                result = proxy.ReportTCResult(testCaseId, testPlanId, status, platformName: projectData.Platform, notes: notes.ToString(), buildid: testBuildId);
            else
                result = new GeneralResult("Invalid Connection", false);
            return result;
        }

        public int GetPlatformId(int testPlanId, string platformName)
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

        /// <summary>
        /// get a test case id. If the test case does not exist then create one
        /// </summary>
        /// <param name="testName"></param>       
        /// <param name="testDescription"></param>
        /// <returns>a valid test case id or 0 in case of failure</returns>
        public int GetTestCaseId(string testName, string testDescription) 
        {
            int TCaseId = getTestCaseByName(testName, testSuiteId);
            if (TCaseId == 0)
            {
                // need to create test case
                GeneralResult result = proxy.CreateTestCase(connectionData.User, testSuiteId, testName, testProjectId,
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
                string externalId = string.Format("{0}-{1}", currentProject.prefix, tcExternalId);
                int featureId = proxy.addTestCaseToTestPlan(currentProject.id, testPlanId, externalId, result.additionalInfo.version_number, platformId);
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
            AllTestPlans = new List<TestPlan>();
            try
            {
                allProjects = proxy.GetProjects();
            }
            catch (TestLinkException tlex)
            {
                lastException = tlex;
                log.ErrorFormat("Failed to connect to TestLink at {1}. Message was '{0}'", tlex.Message, connection.Url);
                return false;
            }
            return true;
        }

        /// <summary>
        /// update selected bits of the testlink fixture data
        /// </summary>
        public bool UpdateProjectData(TestLinkProjectData newProjectData)
        {
            if (basicConnectionValid == false)
            {
                testProjectId = 0;
                testPlanId = 0;
                testSuiteId = 0;
                testBuildId = 0;
                projectDataValid = false;
                return false;
            }
            if (projectData == null)
            {
                projectData = new TestLinkProjectData();
            }

            if (projectData.Project != newProjectData.Project)
            {
                testProjectId = 0;
                AllTestPlans = new List<TestPlan>();
                foreach (TestProject project in allProjects)
                {
                    if (project.name == newProjectData.Project)
                    {
                        currentProject = project;
                        testProjectId = project.id;
                        AllTestPlans = proxy.GetProjectTestPlans(project.id);
                        break;
                    }
                }
                if (testProjectId == 0)
                {
                    testPlanId = 0;
                    testSuiteId = 0;
                    log.ErrorFormat("Test Project '{0}' was not found in TestLink", newProjectData.Project);
                    return false;
                }
            }
            else if (testProjectId == 0) // it was wrong and hasn't changed
                return false;

            if (projectData.Testplan != newProjectData.Testplan)
            {
                testPlanId = 0;
                foreach (TestPlan tp in AllTestPlans)
                    if (tp.name == newProjectData.Testplan)
                    {
                        testPlanId = tp.id;
                        break;
                    }
                if (testPlanId == 0)
                {
                    testSuiteId = 0;
                    log.ErrorFormat("Test plan '{0}' was not found in project '{1}'", newProjectData.Testplan, newProjectData.Project);
                    return false;
                }
            }
            else if (testPlanId == 0) // it was wrong and hasn't changed
                return false;

            if (projectData.Testsuite != newProjectData.Testsuite)
            {
                testSuiteId = GetTestSuiteId(testProjectId, newProjectData.Testsuite, true);
                if (testSuiteId == 0)
                {
                    log.ErrorFormat("Test suite '{0}' was not found in project '{1}'", newProjectData.Testsuite, newProjectData.Project);
                    return false;
                }
            }
            else if (testSuiteId == 0) // it was wrong and hasn't changed
                return false;

            if (projectData.Platform != newProjectData.Platform)
            {
                platformId = GetPlatformId(testPlanId, newProjectData.Platform);
                if (platformId == 0)
                {
                    log.ErrorFormat("Platform '{0}' was not found project '{1}' or is not assigned to testplan '{2}'", newProjectData.Platform, newProjectData.Project, newProjectData.Testplan);
                    return false;
                }
            }
            else if (platformId == 0) // it was wrong and hasn't changed
                return false;

            if (projectData.Build != newProjectData.Build)
            {
                List<Build> builds;
                Build buildToBeUsed = null;

                builds = proxy.GetBuildsForTestPlan(testPlanId);
                if (builds.Count == 0)
                {
                    log.ErrorFormat("No builds available for project '{0}' and testplan '{1}'!", newProjectData.Project, newProjectData.Testplan);
                    return false;
                }
                if ((newProjectData.Build != null) && (newProjectData.Build != ""))
                {
                    foreach (Build b in builds)
                    {
                        if (b.name == newProjectData.Build)
                        {
                            testBuildId = b.id;
                            buildToBeUsed = b;
                            break;
                        }
                    }
                }
                else
                {
                    /* use latest build */
                    testBuildId = builds[builds.Count - 1].id;
                    buildToBeUsed = builds[builds.Count - 1];
                    log.Debug("Using default/latest build: " + buildToBeUsed.name);
                }
                if (buildToBeUsed == null)
                {
                    log.Error("Build " + newProjectData.Build + " not found!");
                    return false;
                }
                else if (!buildToBeUsed.active || !buildToBeUsed.is_open)
                {
                    log.Error("Build " + newProjectData.Build + " not active/open!");
                    return false;
                }

            }
            else if (testBuildId == 0) // it was wrong and hasn't changed
                return false;

            projectData = newProjectData;
            projectDataValid = true;
            return projectDataValid;
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
                // testsuite must exist. Currently no way of creating them
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
