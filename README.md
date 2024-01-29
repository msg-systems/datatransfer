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
- Because of open source extensible to your needs
- Very few dependencies to other libraries

### Quick Start
- Decide which version to use (.Net 4.8 or 7.0) -> install respective runtime if not installed already
- Download binaries
- Write your first job (see documentations, examples, xsd-file)
- Start/Test the tool: datatransfer.exe -f <fullpath to your job.xml>
- to get more debug infos: datatransfer.exe -f <fullpath to your job.xml> -d

### Dependencies
Datatransfer .Net4 depends on 
- DevLib.csv
- Newtonsoft.JSON
Datatransfer .Net7 depends additionally on
- Google.ProtoBuf

Depending of your usage: the third party ADO.Net drivers used

### Structure of this repository
- .net7 : Binaries for the datatransfer .NET version 7.0
- .net48 : Binaries for the datatransfer .NET version 4.8
- CryptPassword : A tool to create base64 encrypted strings with System.Security.Cryptography.ProtectedData, which can be used in the job description files
- 
