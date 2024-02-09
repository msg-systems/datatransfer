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
- TransferTableJob/@sourceTable or alternative TransferTableJob/customSourceSelect
- TransferTableJob/@targetTable
- TransferTableJob/@identicalColumns or TransferTableJob/ColumnMap

With these attributes/elements the source data set and target table is defined. The name of sourceTable/targettable should be in the native way the data provider needs. For example Excel tables are referenced with [SourceSheet$A:D] (Sheet SourceSheet column A to D).

The attribute identicalColumns starts by checking all columns in the source (or customSourceSelect) and assumes in the target table are identical named columns too. The target can have more columns too.

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

## Synchronizing and merging

[Back to index](docIndex.md)