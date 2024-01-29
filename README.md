# datatransfer
A console command tool for extracting, transforming and loading data in diffrent database systems and data format types.

### Description
The datatransfer is an ETL console command tool. Its using job config files to define its jobs. 
It supports ADO.Net data sources, LDAP and some file data formats like JSON, XML or CSV. Non-SQL data sources can be used with SQL similar syntax. 
Main features are 
- ADO.Net data sources; ODBC, OLE, provider for DB2, MSSQL, Oracle, ...
- nonSQL (XML, JSON, CSV, LDAP)
  - SQL like syntax: Joins, where statements, DSL-language for expressions, loading from files and URLs
- HCL Notes data sources
- workflow functions: Pre/Post Statements, PreConditions, check % data amount diffrence between source/target with abort, working with staging tables, variables
- Parallelism
- Transaction handling
- Synchronizing of data rows/columns with subsets on both on source and target
- Synchronizing with timespan columns for only newest data
