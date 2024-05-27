[Back to index](docIndex.md)

# Tutorials and descriptions for specific data sources

This section shows some simple examples for diffrent data source connections. 
Details on connection strings should be checked on the manufacturer site of the ADO driver, if applies.
Details on xml elements and attributes can be on the [Transferjob page](TransferJob.md).

The examples are always the shortest version of an implementation.
For each example the own [SQL-parser/DSL of dataTransfer](DSL.md) can be used.

## using ADO transfers

ADO drivers are mostly published by the manufacturer of a database system and have to be known/registered by dataTransfer.
For this they have to be referenced in the build and maybe also added to the DbProviderFactories for .Net5+.
All samples here use providers which should work already in .NET4 and mostly in .NET5+.

For all examples you should ensure that user names and passwords are correct and you have network connectivity to the data source on the correct port.

### MSSQL

Microsoft SQL Server is a relational database system from Microsoft. SQL Compact DBs or (localDBs) are supported as well.
DataTransfer is fully supported for batch commands ([@targetMaxBatchSize](TransferJob.md#batch-size)), merges ([@merge](TransferJob.md#merging)), syncs ([@sync](TransferJob.md#synchronizing)) and so on.
Used ado driver name is "System.Data.SqlClient".

```
<TransferJob xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation = "job.xsd">
  <transferBlock name="myBlock" 
    conStringSourceType="System.Data.SqlClient" conStringSource="Data Source=myMSSQLDB.myDomain.com;Initial Catalog=SourceDB;User=myUser;Password=Secret;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False"
    conStringTargetType="System.Data.SqlClient" conStringTarget="Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=TargetDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False">

      <TransferTableJob sourceTable="dbo.baseTable" targetTable="dbo.targetTable" sync="false" deleteBefore="true" identicalColumns="true" maxRecordDiff="10.0" />

    </transferBlock>
</TransferJob>
```

### DB2

IBM DB2 is a relational database system from IBM. DB2 Express is supported as well.
DataTransfer is fully supported for batch commands ([@targetMaxBatchSize](TransferJob.md#batch-size)) syncs ([@sync](TransferJob.md#synchronizing)) and so on, except merges([@merge](TransferJob.md#merging)).
Merges can still be used via [post statements](TransferJob.md#pre-and-post-SQL-statements) in native DB2-SQL .
Used ado driver name is "IBM.Data.DB2". A DB2 client/ driver package or server has to be installed for the .NET4 implementation of data transfer. 
For the .NET5+ implementation it could be possible? that this installation is not needed, but this was never tested. With installed DB2 driver/client it works for sure.

```
<TransferJob xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation = "job.xsd">
  <transferBlock name="myBlock" 
    conStringSourceType="IBM.Data.DB2" conStringSource="Server=servername:50000;Database=dbname;UserID=db2admin;pwd=secret"
    conStringTargetType="IBM.Data.DB2" conStringTarget="Server=servername2:50000;Database=dbname2;UserID=db2admin;pwd=secret">

      <TransferTableJob sourceTable="dbo.baseTable" targetTable="dbo.targetTable" sync="false" deleteBefore="true" identicalColumns="true" maxRecordDiff="10.0" />

    </transferBlock>
</TransferJob>
```

### MySql/MariaDB

MySql (Oracle) or Maria DB (open) is a relational database system. Both database types are supported.
DataTransfer is fully supported for batch commands ([@targetMaxBatchSize](TransferJob.md#batch-size)) syncs ([@sync](TransferJob.md#synchronizing)) and so on, except merges([@merge](TransferJob.md#merging)).
Merges can still be used via [post statements](TransferJob.md#pre-and-post-SQL-statements) in native MySQL SQL.
Used ado driver name is "MySql.Data.MySqlClient". 

```
<TransferJob xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation = "job.xsd">
  <transferBlock name="MySqlTest" 
	 conStringSourceType="MySql.Data.MySqlClient" conStringSource="Server=mySQLDDB;Database=dbname;Uid=username;Pwd=Geheim"
	 conStringTargetType="MySql.Data.MySqlClient" conStringTarget="Server=mySQLDDB;Database=dbname;Uid=username;Pwd=Geheim">

     <TransferTableJob sourceTable="dbo.baseTable" targetTable="dbo.targetTable" sync="false" deleteBefore="true" identicalColumns="true" maxRecordDiff="10.0" />

    </transferBlock>
</TransferJob>
```

### Oracle
Oracle is a relational database system from Oracle. 
DataTransfer is fully supported for batch commands ([@targetMaxBatchSize](TransferJob.md#batch-size)) syncs ([@sync](TransferJob.md#synchronizing)) and so on, except merges([@merge](TransferJob.md#merging)).
Merges can still be used via [post statements](TransferJob.md#pre-and-post-SQL-statements) in native Oracle SQL.
Native ado driver name in .Net 4 is "System.Data.OracleClient". "System.Data.OleDb" is possible as well, if ODAC is installed.
Newer drivers are available at oracle, but using new driver names and dlls, which have to be referenced in dataTransfer first.

DataTransfer is only tested with an OleDB-Connection with installed ODAC driver yet.

```
<TransferJob xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation = "job.xsd">
  <transferBlock name="OracleTest" 
	 conStringSourceType="System.Data.OleDb" conStringSource="Provider=OraOLEDB.Oracle;Data Source=(DESCRIPTION=(CID=GTU_APP)(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=servername)(PORT=1521)))(CONNECT_DATA=(SID=PROSEWU)(SERVER=DEDICATED)));User Id=username;Password=geheim"
	 conStringTargetType="System.Data.OleDb" conStringTarget="Provider=OraOLEDB.Oracle;Data Source=(DESCRIPTION=(CID=GTU_APP)(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=servername)(PORT=1521)))(CONNECT_DATA=(SID=PROSEWU)(SERVER=DEDICATED)));User Id=username;Password=geheim">

     <TransferTableJob sourceTable="dbo.baseTable" targetTable="dbo.targetTable" sync="false" deleteBefore="true" identicalColumns="true" maxRecordDiff="10.0" />

    </transferBlock>
</TransferJob>
```

### OLEDB

OLEDB is a programming interface from Microsoft to access database systems. These drivers can only used in Windows operating system.
Native ado driver name in .Net 4 is "System.Data.OleDb".
The syntax of the connection string depends on the used database driver.

#### Access

Access is a file based self hosted database system from Microsoft. 
Batch commands ([@targetMaxBatchSize](TransferJob.md#batch-size)) are not supported but syncs ([@sync](TransferJob.md#synchronizing)) are. 
Merges can still be used via [post statements](TransferJob.md#pre-and-post-SQL-statements) in native Access SQL.
The Ole driver for access (access database engine 2016) has to be installed in the correct architecture (x86/x64) to get it working.
The OLE provider name, which I already used, is "Microsoft.Jet.OLEDB.4.0".

```
<TransferJob xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation = "job.xsd">
  <transferBlock name="AccessTest" 
	 conStringSourceType="System.Data.OleDb" conStringSource="Provider=Microsoft.Jet.OLEDB.4.0;Data Source=TestData\Export-Bearbeitergruppen.mdb;Persist Security Info=True"
	 conStringTargetType="System.Data.OleDb" conStringTarget="Provider=Microsoft.Jet.OLEDB.4.0;Data Source=TestData\Export-Bearbeitergruppen.mdb;Persist Security Info=True">

     <TransferTableJob sourceTable="dbo.baseTable" targetTable="dbo.targetTable" sync="false" identicalColumns="true" maxRecordDiff="10.0" />

    </transferBlock>
</TransferJob>
```

#### Excel

Excel is a popular file based self hosted table calculation system from Microsoft. 
Batch commands ([@targetMaxBatchSize](TransferJob.md#batch-size)) are not supported but syncs ([@sync](TransferJob.md#synchronizing)) are. 
Merges can still be used via [post statements](TransferJob.md#pre-and-post-SQL-statements) in native Excel SQL.
Transactions are not supported.
The Ole driver for excel (access database engine 2016) has to be installed in the correct architecture (x86/x64) to get it working.
The OLE provider name, which I already used, is "Microsoft.ACE.OLEDB.12.0".

```
<TransferJob xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation = "job.xsd">
  <transferBlock name="AccessTest" 
	 conStringSource="Provider=Microsoft.ACE.OLEDB.12.0;Data Source=TestData\ExcelFile.xlsx;Extended Properties=&quot;Excel 12.0 Xml;HDR=YES&quot;;"
	 conStringTargetType="Custom.CSV" conStringTarget="" disableTransaction="true">

     <TransferTableJob sourceTable="[SourceSheet$A:D]" targetTable="export.csv" sync="false" identicalColumns="true" maxRecordDiff="10.0"/>

    </transferBlock>
</TransferJob>
```

### LDAP

LDAP is a popular directory service protocol. 
Batch commands ([@targetMaxBatchSize](TransferJob.md#batch-size)) are not supported but syncs ([@sync](TransferJob.md#synchronizing)) are. 
Merges cannot be used.
Transactions are not supported.
On Windows clients no additional drivers are needed.
The OLE provider name, which I already used, is "ADSDSOObject".
I tested it only with reading data.

```
<TransferJob xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation = "job.xsd">
  <transferBlock name="LDAPTest" 
	 conStringSourceType="Custom.Import.LDAP" conStringSource=""
	 conStringTargetType="Custom.CSV" conStringTarget="" disableTransaction="true">

     <TransferTableJob sourceTable="LDAP://myLDAPServer:636/dc=myDC" sourceWhere = "objectCategory='user' and company = 'msg systems ag'"
                       targetTable="export.csv" sync="false" identicalColumns="false">
       <columnMap>
		 <TransferTableColumn sourceCol="cn" targetCol="cn"/>
       </columnMap>
     </TransferTableJob>

    </transferBlock>
</TransferJob>
```

### ODBC

ODBC can be used with the conStringSourceType = "System.Data.Odbc".
Capabilities depend on the used ODBC driver.
Own experience exists only for the Notes ODBC driver where source tables are Notes views or folders.

Important: ODBC divers has to be installed and have to match the architecture of the dataTransfer binaries.

```
<TransferJob xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation = "job.xsd">
  <transferBlock name="LDAPTest" 
	 conStringSourceType="System.Data.Odbc" conStringSource="Driver={Lotus Notes SQL Driver (*.nsf)};Server=notesserver/domain;database=applic/msg0001.nsf;Uid=Login/msg;Pwd=secret"
	 conStringTargetType="Custom.CSV" conStringTarget="" disableTransaction="true">

     <TransferTableJob sourceTable="IMP_JobModelEntries" targetTable="C:\temp\blabla.csv"
						deleteBefore="false" identicalColumns="true">
    </transferBlock>
</TransferJob>
```

### Other ADO sources

Other ADO sources can be added with the following steps

1. Load and install the ADO driver from the manufacturer
2. Open the source code of the respective dataTransfer project (.net4 or .net5) in Visual Studio 
3. Add the dll for the ADO driver to project reference (best practice is to copy it there before)
4. If you are changing the .NET 5+ version, you have to add a line to Program.cs
4.1. Search for ``` DbProviderFactories.RegisterFactory ```
4.2. Add ``` DbProviderFactories.RegisterFactory("[ADO-Drivername]", [Instanceclass of provider]); ```
5. Compile the new version of dataTransfer and just use it

## non ADO/custom transfers

Other data sources can be queried and written as well. Some are already implemented. Operations like delete or update are not implemented for most data sources. Nevertheless file formats can overwrite the complete file.
Transactions and or batching are not supported. You can implement them if you know how to do so for the concrete data source.

### LDAP custom

A custom LDAP provider is implemented. Main reason for this is the limit of the maximum of 1500 members of a group by one LDAP-Query. 
The custom SQL syntax used is mixed:
- SELECT-part in [DSL](DSL.md)
- FROM-part as LDAP URL with Port
- Where-part as native LDAP-Query
You can use these queries as a whole [customSourceSelect](TransferJob.md#transfertablejob) or with the use of the seperate attributes like [sourceTable, sourceWhere and columnMap](TransferJob.md#transfertablejob).

Insert/update/delete for LDAP is not implemented. 

Custom LDAP can be used with the conStringSourceType = "Custom.Import.LDAP". The conString is empty or can contain any info you want.
To use LDAPS don´t write ldaps://. Instead just use the port 636 in the from-part.

For the DSL some special functions are implemented.

| Name | Parameter | Description |
|------|-----------|-------------|
|coctet|LDAP Octet-number property [input]<br/>Returns regular number|Converts [input] to regular integer and returns it.|
|cdate8|LDAP date8 property [input]<br/>Returns regular date|Converts [input] to regular date and returns it. If not possible an error is returned.|
|cdategen|LDAP default date property [input]<br/>Returns regular date|Converts [input] to date and returns it. If not possible an error is returned.|
|sid|LDAP SID property [input]<br/>Returns SecurityIdentifier|Converts [input] to SecurityIdentifier and returns it. If not possible an error is returned.|
|loadbigarray|Property with more than 1500 elements [input]<br/>Returns list of member|Loads property [input] with all members.|
|splitRows|member array [input]<br/>Returns records|Splits the input array property to seperate records. Only used for array properties|

Example of a query
```
<TransferTableJob targetTable="C:\temp\exportFile.csv" identicalColumns="true">
					
	<!-- Variante CustomSelect-->
	<customSourceSelect> 
		SELECT cn, 
		givenName + ' ' + sn as fullname, 
		cint(userAccountControl &amp; 2)&gt;0 as disbaled,
		cdate8(lastLogon) as lastLogon,
		cDateGen(WhenCreated) as created,
		if(givenname = 'someValue', 'this is someValue', 'this is another one') as MyIdentifier,
		cdate8(accountExpires) as Expires,
		splitRows(proxyAddresses) as proxyAddress
		FROM LDAP://myLDAPServer:636/OU=myOU,dc=myDC
		WHERE (&amp;(objectCategory=user)(company=myCompany)(!userAccountControl:1.2.840.113556.1.4.803:=2))
	</customSourceSelect>

</TransferTableJob>
```

### CSV

The custom CSV provider can read CSV from any source like file or URL. Insert is supprted. Update/delete not.
Export files can be overwritten as a whole.

To use it set conStringSourceType to one of these "Custom.Export.CSV" / "Custom.CSV". 
You can define delmiter and bracing characters global in the conString attribute.
- delimiter: delmiter character - default ;
- enclose: brace character - default "
If bracing is defined, tokens without braces are still recognized.

You can also set delimiter and braces on each from-part of the query like ``` delimiter:,:::enclose:#:::C:\mycsv.csv ```

Transactions are not supported.
The custom SQL works with expression language in select- and where-part and in the on-conditions of joins.

i.e.
```
<TransferTableJob targetTable="C:\temp\hardware5.csv" identicalColumns="true">
		<customSourceSelect>
			<![CDATA[
			SELECT t1.KEY as s, t1.LIEFERANT, t2.BETRIEBSSYSTEM, t1.KEY + ' x ' + t1.MEMORY_EINHEIT as test, 'test' as test2
			FROM C:\temp\hardware.csv as t1 inner join C:\temp\hardware.csv as t2 on t1.KEY = t2.KEY
			WHERE t1.KEY <> '' && t2.KEY <> '' and startsWith(t1.KEY, 'h-')
			]]>
		</customSourceSelect>
	</TransferTableJob>
</transferBlock>
```

### XML
#### HCL Notes Domino ReadViewEntries
### JSON

[Back to index](docIndex.md)