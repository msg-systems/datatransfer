[Back to index](docIndex.md)
# Command line parameters and usage for dataTransfer.exe 

The data transfer is a simple command line interface (CLI) tool with only a few parameters to execute transfer jobs written in the specification of an [xsd-schema](en/job.xsd). if you start datatransfer without any parameters, it is assumed that the job file to process is called job.xml and is in the same path as the executable.

To fully understand all parameters, it is recommended to first understand the [General structure](TransferJob.md#General-structure) of a transfer job.
DataTransfer.exe creates log files stored in the subdirectory Log. 

## Parameters

| Parameter     | Type  | Usage       |
| ------------- | ----- | -------------|
| -f |string (list)| Declaration of which transfer job files should be proccessed. Multiple can be separated with , . If omitted job.xml is assumed. |
| -b |string (list)| Declaration which transfer block of a single job file should be processed. Reference of blocks by name. Only usable if exact one transfer job is processed. Multiple blocks can be separated by , . If omitted all blocks are processed. |
| -j |string (list)| Declaration of which table job in a single transfer block should be processed. Reference of the table job by the name of the attribute targetTable. Only usable if exact one block is processed. Multiple table jobs can be separated by , . If omitted all jobs are processed. |
| -l |string| Sets the name pattern of the log file. The pattern follows [DateTime-Formatting of .NET](https://learn.microsoft.com/de-de/dotnet/standard/base-types/custom-date-and-time-format-strings). You can set relative or absolute paths to change the default path. Default is global_[yyyy.MM.dd_HH.mm.ss].log . |
| -sl |flag |Use Sublogs - creates for every inputfile an own log file - Default false. Name of the sublog is defined in each data transfer job XPAth: TransferJob/settings/@writeLogToFile . Default false |
| -s |flag| Switches from parallel processing for every transfer block to sequential processing. Default false |
| -d |flag| Starts the datatransfer in debug mode. Debug mode creates really verbose logs and every SQL/operation is executed and logged separate. The MaxBarchsize (TransferJob/TransferBlock/@MaxBatchSize) is set to 1. |
| -h/-? |flag| Shows help on parameters similar to this page |

## Examples

For this example we assume the following job file names myJob.xml within the same directory as the datatransfer.exe :
``` 
<TransferJob xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation = "job.xsd">
  <transferBlock name="block1" ...>
	<TransferTableJob targetTable="tab1" .../>
	<TransferTableJob targetTable="tab2" .../>
	<TransferTableJob targetTable="tab3" .../>
  </transferBlock>
  <transferBlock name="block2" ...>
	<TransferTableJob targetTable="tab4" .../>
	<TransferTableJob targetTable="tab5" .../>
  </transferBlock>
  <transferBlock name="block3" ...>
    <TransferTableJob targetTable="tab6" .../>
  </transferBlock>
</TransferJob>
```

### Basic job execution

To start the job just type
``` 
> DataTransfer.exe -f myJob.xml
``` 
This will start the job, execute block1, block2 and block3 in parallel. All jobs are executed in the defined order.

To start only block1 or i.e. 2 blocks 
``` 
> DataTransfer.exe -f myJob.xml -b block1
> DataTransfer.exe -f myJob.xml -b block1,block2
``` 

To start only job tab1 and tab3 of block1
``` 
> DataTransfer.exe -f myJob.xml -b block1 -j tab1,tab3
``` 

### Advanced parameters for job exection

To change the name or path of the log file
``` 
> DataTransfer.exe -f myJob.xml -l myLogformat_[HH.mm.ss].csv
> DataTransfer.exe -f myJob.xml -l C:\mylogpath\myLogformat_[HH.mm.ss].csv
``` 

To process jobs in order
``` 
> DataTransfer.exe -f myJob.xml -s
``` 
This will process the blocks in the order of appearance - block1, block2, block3

To start the debug mode
``` 
> DataTransfer.exe -f myJob.xml -d
``` 

[Back to index](docIndex.md)