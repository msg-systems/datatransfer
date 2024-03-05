[Back to index](docIndex.md)

# Tutorials and descriptions for specific data sources

This section shows some simple examples for diffrent data source connections. 
Details on connection strings should be checked on the manufacturer site of the ADO driver if applies.

## using ADO transfers

ADO drivers are mostly published by the manufacturer of a database system and have to be known/registered by dataTransfer.
For this they have to be referenced in the build and maybe also added to the DbProviderFactories for .Net5+.
All samples here use providers which should work already in .NET4 and mostly in .NET5+.

For all examples you should ensure that user names and passwords are correct and you have network connectivity to the data source on the correct port.

### MSSQL

Microsoft SQL Server is a relational database system from Microsoft. SQL Compact DBs or (localDBs) are supported as well.
DataTransfer is fully supported for batches, merges, syncs and so on.
Used ado driver name is "System.Data.SqlClient".

```
<TransferJob xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation = "job.xsd">
  <transferBlock name="myBlock" 
    conStringSourceType="System.Data.SqlClient" conStringSource="Data Source=myMSSQLDB.myDomain.com;Initial Catalog=SourceDB;User=myUser;Password=Secret;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False"
    conStringTargetType="System.Data.SqlClient" conStringTarget="Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=TargetDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False">

      <TransferTableJob sourceTable="dbo.baseTable" targetTable="fbo.targetTable" sync="false" deleteBefore="true" identicalColumns="true" maxRecordDiff="10.0" />

    </transferBlock>
</TransferJob>
```

### DB2
### MySql/MariaDB
### Oracle
### OLEDB
#### Access
#### Excel
### LDAP
### ODBC
### Other ADO sources
## non ADO/custom transfers
### LDAP custom
### CSV
### XML
#### HCL Notes Domino ReadViewEntries
### JSON

[Back to index](docIndex.md)