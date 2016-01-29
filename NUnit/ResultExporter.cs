/* 
Nunit TestLink Adapter 
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
using System.Text;
using NUnit.Core.Extensibility;
using NUnit.Core;
using NUnit.Framework;
using System.Reflection;
using System.IO;
using log4net;
using log4net.Repository.Hierarchy;
using log4net.Core;
using log4net.Appender;
using log4net.Layout;
using log4net.Filter;

namespace Meyn.TestLink.NUnitExport
{
    /// <summary>
    /// this class is installed by the AddIn and in turn is called by the 
    /// NUnit execution framework when the tests have been run. For any 
    /// NUnit testfixtures that have the TestLinkFixture attribute it reports
    /// the test results back to Testlink
    /// </summary>
    public class ResultExporter:EventListener
    {

        /// <summary>
        /// stores the testLinkFixtureAttributes against class names
        /// </summary>
        public Dictionary<string, TestLinkFixtureAttribute> fixtures = new Dictionary<string, TestLinkFixtureAttribute>();       

        /// <summary>
        ///  handles all comms to Testlink
        /// </summary>
        TestLinkAdaptor adaptor;

        private string currentTestOutput  = "";
        private const string defaultConfigFile = "tlinkconfig-default.xml";
        private TestLinkFixtureAttribute defaultTlfa;

        /// <summary>
        /// uses the Nunit trace facility. To set the trace levels
        /// you need to modify the nunit-console.exe.config file
        /// </summary>
        private readonly ILog log;

        private void SetupLogger()
        {
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
            PatternLayout patternLayout = new PatternLayout();
            patternLayout.ConversionPattern = "%date [%thread] %-5level [%class.%method] - %message%newline";
            patternLayout.ActivateOptions();
            RollingFileAppender roller = new RollingFileAppender();
            roller.AppendToFile = false;
            roller.File = @"ResultExporterLog.txt";
            roller.Layout = patternLayout;
            roller.MaxSizeRollBackups = 5;
            roller.MaximumFileSize = "1GB";
            roller.RollingStyle = RollingFileAppender.RollingMode.Size;
            roller.StaticLogFileName = true;
            roller.ActivateOptions();
            ConsoleAppender console = new ConsoleAppender();
            PatternLayout consolePatternLayout = new PatternLayout();
            consolePatternLayout.ConversionPattern = "%date [Testlink Result Exporter] [%level] %message%newline";
            consolePatternLayout.ActivateOptions();
            LevelRangeFilter consoleLevelFilter = new LevelRangeFilter();
            consoleLevelFilter.LevelMin = Level.Info;
            consoleLevelFilter.LevelMax = Level.Fatal;
            console.AddFilter(consoleLevelFilter);
            console.Layout = consolePatternLayout;
            hierarchy.Root.AddAppender(roller);
            hierarchy.Root.AddAppender(console);
            hierarchy.Root.Level = Level.All;
            hierarchy.Configured = true;
        }

        public ResultExporter()
        {
            SetupLogger();
            log = LogManager.GetLogger(typeof(TestLinkAdaptor));
            adaptor = new TestLinkAdaptor(log);
            defaultTlfa = new TestLinkFixtureAttribute();
            defaultTlfa.ConfigFile = defaultConfigFile;
            if (!(defaultTlfa.ConsiderConfigFile(Directory.GetCurrentDirectory())))
            {
                log.Debug("Default config file not found!");
                defaultTlfa = null;
            }
        }

        #region EventListener Overrides
        public void RunStarted(string name, int testCount)
        {
        }

        /// <summary>
        /// called at the end. Here we export the results to test link
        /// </summary>
        /// <param name="result"></param>
        public void RunFinished(TestResult result)
        {
            log.InfoFormat("Test execution finished, starting exporter");
            processResults(result);
            log.InfoFormat("Exporter finished!");
        }

 
        public void RunFinished(Exception exception)
        {
        }
        public void TestStarted(TestName testName)
        {
            currentTestOutput = "";
        }
        /// <summary>
        /// a test has finished. 
        /// </summary>
        /// <param name="result"></param>
        public void TestFinished(TestResult result)
        {
        }

        public void SuiteStarted(TestName testName)
        {
        }
        public void SuiteFinished(TestResult result)
        {
        }
        public void UnhandledException(Exception exception)
        {
        }
        /// <summary>
        /// capture any console output during the testing.
        /// </summary>
        /// <param name="testOutput"></param>
        public void TestOutput(TestOutput testOutput)
        {
            currentTestOutput = testOutput.Text;
        }

        #endregion
        /// <summary>
        /// extracts the type name of the test fixture name.
        /// Assumes the test fixture name is the fully qualified testmethod name.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string extractTestFixture(string path)
        {
            log.Debug(String.Format("Extracting Test Fixture from '{0}'", path));
            int index = path.LastIndexOf(".");
            if (index < 1)
                return "";

            string candidate = path.Substring(0, index);
            return candidate;
        }

        private string lastTestFixtureName = "";


        /// <summary>
        /// parse results and sub results. If it is a test case then try to record it in testlink
        /// </summary>
        /// <param name="result"></param>
        private void processResults(TestResult result)
        {
            log.DebugFormat("Process results for '{0}'", result.Name);
            if (IsDllPath(result.Name))
                extractTestFixtureAttribute(result.Name);

            if (result.HasResults)
            {

                foreach (TestResult subResult in result.Results)
                {
                    log.DebugFormat("Going recursive into '{0}'", subResult.Name);
                    processResults(subResult);
                }
            }
            else 
            {
               
                string testFixtureName = extractTestFixture(result.FullName);
                log.Debug(string.Format("Processing results for test {0} in fixture: {1}",result.Name, testFixtureName));
                if (fixtures.ContainsKey(testFixtureName))
                {
                    Meyn.TestLink.TestLinkFixtureAttribute tlfa = fixtures[testFixtureName];
                    
                    if (tlfa.TestSuite == null)
                    {
                        /* if testuite is not defined in default config file, take the Fullname as Testsuite name */
                        tlfa.TestSuite = extractTestFixture(result.FullName);
                    }
                    if (tlfa.ExportEnabled)
                    {
                        reportResult(result, tlfa);
                    }
                    else
                    {
                        log.Warn("Export skipped as enable parameter is set to false or missing");
                    }
                }
            }
        }

        private bool IsDllPath(string path)
        {
            bool result = (path.ToLower().EndsWith(".dll"));
            return result;
        }

        /// <summary>
        /// gather all the necessary information prior to 
        /// reporting the results to testlink.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="tlfa"></param>
        private void reportResult(TestResult result, Meyn.TestLink.TestLinkFixtureAttribute tlfa)
        {
            adaptor.ConnectionData = tlfa; // update the connection and retrieve  key base data from testlink
 
            try
            {
                string TestName = result.Name;
                string MethodName = result.Test.MethodName;
                if (!TestName.Equals(MethodName))
                {
                    
                    /* In case of parameterized tests, result.Name only contains the name of the parameter. So add the name of the actual test (=methodname) also */
                    TestName = MethodName + "." + TestName;
                }
                
                string TestDescription = result.Description;
                if (TestDescription == null)
                {
                    TestDescription = "";
                }

                if (adaptor.ConnectionValid == false)
                {
                    log.WarnFormat(string.Format("Failed to export tesult for testcase {0}", result.Name));
                    return;
                }

                try
                {
                    int TCaseId = adaptor.GetTestCaseId(TestName, TestDescription);
                    log.Error(string.Format("Exporting result for testcase {0}", result.Name));
                    if (TCaseId > 0)
                    {
                        sendResultToTestlink(result, tlfa, TCaseId);
                    }
                }
                catch (TestLinkException tlex)
                {
                    log.Error(string.Format("Failed to export testcase '{0}'. {1}", TestName, tlex.Message));
                }
            }
            catch (TestLinkException tlex)
            {
                log.Error(tlex.Message, tlex);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
        }

        /// <summary>
        /// after everything has been setup, record the actual result.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="tlfa"></param>
        /// <param name="testPlanId"></param>
        /// <param name="TCaseId"></param>
        private void sendResultToTestlink(TestResult tcResult , TestLinkFixtureAttribute tlfa,  int TCaseId)
        {
            TestCaseResultStatus status = TestCaseResultStatus.Blocked;

            StringBuilder notes = new StringBuilder();
            notes.AppendLine(tcResult.Message);
            notes.AppendLine(currentTestOutput);

            switch (tcResult.ResultState)  //RunState)
            {
                case ResultState.NotRunnable:
                    status = TestCaseResultStatus.Blocked;
                    break;
                case ResultState.Skipped:
                    status = TestCaseResultStatus.Blocked;
                    notes.AppendLine ("++++ SKIPPED +++");
                    break;
                case ResultState.Ignored:
                    status = TestCaseResultStatus.Blocked;
                    notes.AppendLine("++++ IGNORED +++");
                    break;
                case ResultState.Success: status = TestCaseResultStatus.Pass; break;
                case ResultState.Failure: status = TestCaseResultStatus.Fail; break;
                case ResultState.Error: status = TestCaseResultStatus.Fail; break;                  
            }

            GeneralResult result = adaptor.RecordTheResult(TCaseId, status, notes.ToString());
            if (result.status != true)
            {
                log.WarnFormat(string.Format("Failed to export Result. Testlink reported: '{0}'", result.message));
            }
            else
             log.Info(
                string.Format("Reported Result (TCName=\"{0}\", TestPlan=\"{1}\", Status=\"{2}\").",
                tcResult.Name,
                tlfa.TestPlan,
                tcResult.ResultState.ToString()));          
        }

        /// <summary>
        /// load the dll and extract the testfixture attribute from each class
        /// </summary>
        /// <param name="path"></param>
        private void extractTestFixtureAttribute(string path)
        {

            // assembly loading requires an absolute path
            if (Path.IsPathRooted(path) == false)
            {
                DirectoryInfo di = new DirectoryInfo(".");
                path = Path.Combine(di.FullName, path);
            }

            log.Debug(string.Format("Loading assembly '{0}'", path));
            Assembly target = Assembly.LoadFile(path);
            Type[] allTypes = target.GetExportedTypes();

            foreach (Type t in allTypes)
            {
                log.Debug(string.Format("Examining Type {0}", t.FullName));
                TestLinkFixtureAttribute tlfa = null;
                foreach (System.Attribute attribute in t.GetCustomAttributes(typeof(TestLinkFixtureAttribute), false))
                {
                    tlfa = attribute as TestLinkFixtureAttribute;
                }

                if (tlfa == null)
                {
                    if (defaultTlfa != null)
                    {
                        tlfa = (TestLinkFixtureAttribute)defaultTlfa.Clone();
                        log.DebugFormat("Using default config file for test fixture: {0}", t.FullName);
                    }
                    else
                    {
                        log.ErrorFormat("Unable to export results for {0}: No default config file available!", t.FullName);
                    }
                }

                if (tlfa != null)
                {
                    if (!tlfa.ConsiderConfigFile(Path.GetDirectoryName(path)))
                    {
                        tlfa.ConfigFile = defaultConfigFile;
                        /* try again with default config file */
                        tlfa.ConsiderConfigFile(Path.GetDirectoryName(path));
                    }
                    log.DebugFormat("Found fixture attribute for test fixture: {0}", t.FullName);
                    try
                    {
                        tlfa.Validate();

                        if (fixtures.ContainsKey(t.FullName))
                        {
                            fixtures[t.FullName] = tlfa;
                        }
                        else
                        {
                            fixtures.Add(t.FullName, tlfa);
                        }
                    }
                    catch (Exception e)
                    {
                        log.ErrorFormat("Unable to export results for {0}: {1}", t.FullName, e.Message);
                    }
                }
            }
        }


    }
}
