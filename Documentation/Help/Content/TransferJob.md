[Back to index](docIndex.md)

# Transfer job structure

## General structure
Transfer jobs are xml files, which define the source and the target data source of a transfer, called TransferBlock, and the source data and target tables to fill, called TransferTableJob.
Within TransferTableJobs column mappings can be defined to transform data or map columns with diffrent names in the source or target table.
This structure is shown below.

![Basic job structure](en/basic_job_structure.png "Basic job structure")

The job xml is delivered with an XSD schema definition file to help you writing own job files. 
All you need is a xsd capable editor, i.e. Visual Studio Code with [Red Hat XML Plugin](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-xml). 
This enables context help, code completion and some validation, depending on the used editor. 

The xsd file is shipped in english. A [german version](de/job.xsd) is in the content/de part of the documentation.
Referencing the xsd job is done with the following base structure, assuming job.xsd is in the same path.
```
<?xml version="1.0"?>
<TransferJob xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation = "job.xsd">
...
</TransferJob>
```

XPath is used when refering to elements in this documentation.

## Basic functionality

### TransferBlock

Each Transferblock builds a transactional scope for operations between 2 data sources. You have to assign an unique name to each.

#### Data source types

The data transfer uses ADO.Net data sources to connect to diffrent data sources along side some custom added data sources. To define which data source to use, the xml attributes Transferblock/@SourceType and Transferbloc/@TargetType are used.
The names of the data sources are provided by the manufacturer. Examples are

| System     | Type name  | 
| ------------- | ----- |
| MSSQL | System.Data.SqlClient |
| ODBC | System.Data.Odbc |
| OLE | System.Data.OleDb |
| MySql/MariaDB | MySql.Data.MySqlClient |
| CSV | Custom.CSV |
| ... | ... |

```
<transferBlock name="myBlock" 
	conStringSourceType="IBM.Data.DB2" conStringSource="..."
	conStringTargetType="System.Data.SqlClient" conStrintTarget="...">
		...
</transferBlock>
```

Be aware that some database drivers are dependent on the correct build architecture like x86 and x64. 
You have to use a version of datatransfer in the correct architecture to use these data sources.
Sometimes it is also needed to install some software from the manufacturer before the ADO.NET driver is working, i.e. DB2 Data Client for DB2 or ODAC for Oracle.

#### Connection strings

Now that the type of data source is defined, the actual connection data to an instance of this data type has to be specified. This is done with the attributes TransferBlock/@conStringSource and TransferBlock/@conStringTarget. The format of these connection string is defined by the ADO.Net provider. A good help page for these formats are the manufacturer´s help pages or [wwww.connectionstrings.com](https://www.connectionstrings.com/). Passwords in connection strings are omitted in log files.
For custom data source connection strings, please see the tutorial section and the DSL section [defining origins and remote request](DSL.md#Defining-origins-and-remote-request).

Windows only: Because connection strings often have passwords within, it is possible to encrypt these. For encryption Windows ProtectedData (DPAPI) is used. The tool CryptPassword.exe.zip is shipped within the release of datatransfer which converts a password text to UTF8 bytes, then encrypts it with DataProtect (machine/user options) and converts the result to Base64. The resulting base64 strings can be used in connectionstrings for the property names 'pwd', 'Password' and 'password'. 

```
<transferBlock name="myBlock" 
	conStringSourceType="IBM.Data.DB2" conStringSource="Server=databaseserver:50000;Database=dbname;UserID=db2admin;password=Base64PW"
	conStringTargetType="System.Data.SqlClient" conStrintTarget="Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=SQL_DWH;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False">
		...
</transferBlock>
```

### TransferTableJob

The TransferBlock is the context for all TransferTableJobs. In the most simple form TransferTableJobs only have 3 attributes:
- TransferTableJob/@sourceTable (maybe with TransferTavbleJob/@sourceWhere) or alternative TransferTableJob/customSourceSelect
- TransferTableJob/@targetTable
- TransferTableJob/@identicalColumns or TransferTableJob/ColumnMap

With these attributes/elements the source data set and target table is defined. The name of sourceTable/targettable should be in the native way the data provider needs. For example Excel tables are referenced with [SourceSheet$A:D] (Sheet SourceSheet column A to D).

The attribute @identicalColumns starts by checking all columns in the source (or customSourceSelect) and assumes in the target table are identical named columns too. The target can have more columns too.

@sourceWhere and @SourceTable makes most sense if used with identicalColumns, so you can omit a customSourceSelect and a ColumnMap.

```
<TransferTableJob sourceTable="mySource" targetTable="myTarget" identicalColumns="true"/>

<TransferTableJob targetTable="myTarget" identicalColumns="true">
  <customSourceSelect> SELECT col1, col2*3 as col2_m from mySource  </customSourceSelect>
</TransferTableJob>
```

If no identicalColumns are defined, you have to define a columnMap with TransferTableColumn elements, which map source column (customSourceSelect) names to the target column names.
The columnMap or the customSourceSelect allow full usage of the query syntax of the source data source.

```
<TransferTableJob sourceTable="mySource" targetTable="myTarget">
	<columnMap>
		<TransferTableColumn sourceCol="col1" targetCol="col1_othername"/>
		<TransferTableColumn sourceCol="4" targetCol="constantTarget"/>
		<TransferTableColumn sourceCol="cast(replace(col3, ',', '') as integer)" targetCol="col3"/>
		<TransferTableColumn sourceCol="(Select singleVal from Tab2 where Tab2.key = Tab1.Key)" targetCol="col4"/>
	<columnMap>
</TransferTableJob>
```

## Workflow functionality

DataTransfer has only basic workflow functionality and follows a prescribed order in processing. There are just a few spots which are used as injection points for own logic or functionality.

Some of them has to set for specific data sources, i.e. disabling of transaction for data providers which does not support thise.

The basic workflow looks like this:

![TransferBlock workflow](en/Workflow.png "TransferBlock workflow")

### Transaction settings

You can enable/disable transactions on the target data source with TransferTableJob/@disableTransaction. To disable the transactions in generally not recommended, but can be necessary if the target does not support these. I.e. Access or file formats does not support transactions.

With the addition of TransferTableJob/@transactionIsolationLevel you can set the needed isolation level - default is serializable. For more information on transactions and isolation levels please refer to your data source provider documentation.

```
<transferBlock name="myBlock" 
	conStringSourceType="..." conStringSource="..."	conStringTargetType="..." conStrintTarget="..."
	disableTransaction = "false" transactionIsolationLevel = "ReadCommited">
		...
</transferBlock>
```

### Pre-conditions

Pre-conditions can be used on TransferBlock (TransferBlock/preCondition) and/or on TransferTableJob (TransferTableJob/preCondition) level. On TransferBlock level they are once checked for the whole block and on TransferTableJob level before running the specific TransferTableJob.
You can specify as many needed. They are processed in order of appearance.

Each pre-condition can either pe checked on the source or the target data source. This is specified at preCondition/@checkOn.

With preCondition/@retryCount and preCondition/@retryDelay additonal tries can be defined, if the check fails. preCondition/@retryCount sets the amount of retries and preCondition/@retryDelay the seconds to wait between the retries.

To get the date for the check you have to specify a query with preCondition/@select on the data source which returns the values to be checked. If multiple records are returned by your query only the first record is checked.

The check itsself is defined in preCondition/@condition. The syntax is [colname]:[desiredVale]. You can set multiple conditions by seperating them with ;.

```
<transferBlock name="myBlock" 
	conStringSourceType="..." conStringSource="..."	conStringTargetType="..." conStrintTarget="...">
	
	<preCondition checkOn="Target" select="Select count(*) as cnt from MyTab" condition="cnt:0"/>

	<preCondition checkOn="Source" select="Select 1 as result from MyTab where exists(complicated subselect)" condition="result:1"/>

	<TransferTableJob sourceTable="..." targetTable="...">
		<preCondition checkOn="Source" retryCount="5" retryDelay="10" select="Select col1, col2 from sourceTab" condition="col1:test;col2:other"/>
	</TransferTableJob>
</transferBlock>
```

### Record diffrence check

Another special form of condition is the record diffrence check. It checks the record count on the target and compare it with the record count to transfer on source. If the new record has less than a specified percentage of the current target data, the processing is aborted.
The allowed maximum diffrence is defined with TransferTableJob/@maxRecordDiff in percentage. 
This scenario is mostly used for DWH transfers, where the source data is sometimes incomplete and has to be checked before.

```
	<TransferTableJob sourceTable="sourceTab" targetTable="targetTab" maxRecordDiff = "60" />
```

Example: Assume that targetTab has currently 100 records. If maxRecordDiff is 60, the transfer is aborted if there are less than 40 records in sourceTab to transfer, because more than 60% of data would be deleted in the target table.

### Pre and post SQL statements

Pre und post SQL statements can be executed in any amount for each TransferTableJob. They are always executed on the target data source, because of the transactional scope which only exists there. 
They are executed before the transfer starts modifying data and after the transfer is done. Only exception is [merging](TransferJob.md#Synchronizing-and-merging), which is always processed as last step.
These statement can execute any abritary SQL like stored procedures.

```
	<TransferTableJob sourceTable="sourceTab" targetTable="targetTab" >
	  <preStatement> UPDATE tempTab set x='y'</preStatement>
	  <preStatement> Exec logProcess('myLoggingStart') </preStatement>
	  ...
	  <postStatement> Exec logProcess('myLoggingEnd') </postStatement>
	</TransferTableJob>
```

### Deletion before filling

If you just want to transfer data, without synching or merging, you can delete the content of the source table before loading the new data. There is no danger of data loss, if you are using a transaction, because its just rolled back on error. Please be aware that database trigger may fire on deletion.
To activate the deletion use TransferTableJob/@deleteBefore. To restrict the set of deleted data add TransferTableJob/@deleteWhere.

The same can achieved by using a preStatement sql.

```
	<TransferTableJob sourceTable="sourceTab" targetTable="targetTab" deleteBefore = "true" deleteWhere = "type = 'server'" />
```

### Batch size

DataTransfer inserts, updates and deletes records with SQL statements. If 100.000 of records are inserted, the communication overhead for every single insert/update/delete statement can become a bottle neck.
Because of this, DataTransfer assembles single updates/inserts/deletes to batch jobs, so that multiple commands are sent to the server at once. This has big effects on performance but is sometimes a bit complicated to debug, because of error messages which just refer to 1 entry of a 1000 command batch. The drawback can be softened by using the [-d debug parameter in command line](datatransfer.exe.md#Parameters) which sets the batch size for this run to 1.

## Synchronizing and merging

[Back to index](docIndex.md)