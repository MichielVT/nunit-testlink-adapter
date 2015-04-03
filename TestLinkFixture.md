**Update: new argument: ConfigFile**

# Introduction #

The TestLinkFixture Attribute is used to decorate TestFixtures that instucts the TestRunner to export the test results to TestLink.

# Details #

To use this attribute you need to:
  1. provide a reference to the the TestLinkFixture.dll
  1. include a Using statement for Meyn.TestLink

Then decorate your existing TestFixture with this attribute.
You need to provide the following details:
  * Url: where the testlink api is located.
  * ProjectName: what is the name of the test project in testlink to be used
  * TestPlan: the testplan name that will receive the test results
  * TestSuite: the test suite name where the test cases will appear
  * UserId: what is the user name to be used if test cases need to be created first
  * DevKey: the [API Access Key](ApiKey.md) provided by TestLink for the above user id

Example Values
```
        Url = "http://localhost/testlink/lib/api/xmlrpc.php"
        ProjectName = "TestLinkApi"
        UserId = "admin"
        TestPlan = "Automatic Testing"
        TestSuite = "nunitAddOnSampleTests"
        DevKey = "ae28ffa45712a041fa0b31dfacb75e29"
```

Alternatively you can now externalise most or all of this information by  specifying a configuration file. See UsingTheConfigurationFile.