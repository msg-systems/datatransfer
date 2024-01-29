# datatransfer
A console command tool for extracting, transforming and loading data in diffrent database systems and data format types.

### Description
The datatransfer is an ETL console command tool. Its using job config files to define its jobs. 
It supports ADO.Net data sources, LDAP and some file data formats like JSON, XML or CSV. Non-SQL data sources can be used with SQL similar syntax. 

Main features are 
- Available in .net 4.8 (Windows only) and .Net 7 (Win, Mac, Linux - Linux and Mac not tested)
- ADO.Net data sources: ODBC, OLE, provider for DB2, MSSQL, Oracle, ...
- nonSQL: XML, JSON, CSV, LDAP
- nonSQL: SQL like syntax Joins, where statements, DSL-language for expressions, loading from files and URLs
- HCL Notes data sources
- workflow functions: Pre/Post Statements, PreConditions, check % data amount diffrence between source/target with abort, working with staging tables, variables
- Parallelism
- Transaction handling
- Synchronizing of data rows/columns with subsets on both on source and target
- Synchronizing with timespan columns for only newest data
- Logging with debug level and mails on error (if configured)
- Small footprint, small binaries, no application server
- Usable in command line and therefore easy to integrate into other workflow engines
- Job descriptions in xml with existing xsd schema
- Password encryption in job descriptions user/server based
- Because of open source, extensible to your needs
- Very few dependencies to other libraries

### Quick Start
- Decide which version to use (.Net 4.8 or 7.0) -> install respective runtime if not installed already
- Download binaries
- Write your first job (see documentations, examples, xsd-file)
- Start/Test the tool: datatransfer.exe -f <fullpath to your job.xml>
- to get more debug infos: datatransfer.exe -f <fullpath to your job.xml> -d
- Customizing the code is done with Visual Studio Community or higher Visual Studio versions

### Dependencies
Datatransfer .Net4 depends on 
- DevLib.csv
- Newtonsoft.JSON
Datatransfer .Net7 depends additionally on
- Google.ProtoBuf

Depending of your usage: the third party ADO.Net drivers used.

For .Net 4.8: If you don´t use some shipped ADO.Net drivers you can just remove them.

### Structure of this repository
- binaries\.net7 : Binaries for the datatransfer .NET version 7.0
- binaries\.net48 : Binaries for the datatransfer .NET version 4.8
- packages\CryptPassword : A tool to create base64 encrypted strings with System.Security.Cryptography.ProtectedData, which can be used in the job description files
- Documentation\Content : German documentation documents for workflow of processing, UML, DSL documentation and description of structure of the job files
- Documentation\Help : Windows help file and web help for code elements of this project
- UnitTest : Unit Tests
- DataTransfer.Net4 : Source code for datatransfer version 4.8
- DataTransfer.Net5 : Source code for datatransfer version 7.0 (5+)
- msa.DSL : Source code for DSL implementation
- msa.logging : Source code for logging
- msaLotusInterop : Source code for (HCL) Notes interface 

### Customizing
You can customize the code to fulfill your needs. 
- to add an ADO.Net driver just add the driver to the project
  - .Net 4.8 compile it
  - .Net 7 - add a line in program.cs Main method like : DbProviderFactories.RegisterFactory("IBM.Data.DB2", IBM.Data.Db2.DB2Factory.Instance);
- to implement custom logic for a ADO.Net provider
  - Create a class under Database which is inheriting from DBInterface and define the properties supportsDataAdapterCommands, supportsParameter and supportsBatchCommands (in doubt just try)
  - reference this class in DBInterface.cs method getInterface by adding a new case like
  - other features which can be added in your class
    - add methods for createDataAdapter, createDBCommandBuilder if the ADO.Net-Factory class doesn´t support this
    - implement own methods for deletion deleteTableRows
    - implement own methods for merge
    - implement getParamName for custom parameter name format for your data source
    - just look into the existing implementations
- to implement custom logic for non-ADO.Net provider
  - Create a class under Database\Custom which is inheriting from CustomInterfaceBase and define the properties/events for
    - define drivername, transactionality, batchsize ... (see current implementations)
    - define handler for reading: fillFromSQLParseTree
    - define handler for writing: initFillContext, commitFillContext, rollbackFillContext, insertHandler and if needed updateHandler and deleteHandler
    - define if SQL syntax is used and complex from expressions (like http)
    - overwrite base methods if needed like deleteTableRows 
  - reference this class in DBInterface.cs method getInterface by adding a new case like
- to implement custom source-Parameters
  - look into the class Model\RemoteRequest method resolveRequest -> there you can add custom protocols and logic for authenticating
    
