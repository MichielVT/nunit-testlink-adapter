# nunit-testlink-adapter

This is the NUnit testlink adapter which implements an NUnit 2.6.x addon in order to interface a testlink instance.
The project is based and forked from https://github.com/smeyn/gallio-testlink-adapter

##What is the difference to gallio-testlink-adapter?

The support for MBUnit is skipped and only NUnit is supported. Moreover, it implements a UI in order to notify the user about the current testlink communication. (TBD)
 
##Testbench

The included testbench (to test the testlink API) requires an instance running on the local machine. The instance has to have following content:
- First Testproject called "apiSandbox"
- Second Testproject called "Empty TestProject"
- Content of the first Testproject:
-- One testsuite called "business rules"
-- One testplan called "Automated Testing"
