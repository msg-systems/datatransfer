[Back to index](docIndex.md)
# Domain specific language DSL

The dataTransfer is able to define and use variables and query non ADO.NET data sources. 
Because non ADO.NET data sources mostly don´t understand SQL the dataTransfer uses an own [SQL Parser](DSL.md#sql-parser-for-non-sql) and a DSL expression language for [variables](DSL.md#variables), where parts and columns.
Variables can also be used in standard SQL statement of ADO.NET data sources.

There are several variants of DSL expression languages build in, depending on the custom data provider. For [variable initialization](DSL.md#variables) without provider context, the most basic DSL variant is used.
The basic syntax of all DSLs is described here. Special functions are described at the documentation for the [concrete custom provider](DataSourceHelpTutorials.md#non-ado/custom-transfers).

A [pdf documentation](en/Domain%20specific%20language%20definition.pdf) is available too.

## Variables

The Datatransfer allows the usage of variables in TransferTableJobs with the XML element TransferTableJob/variable.
You can define as many variables as you want. The processing is done in order of appearance.
To define or set a variabel use @type and @name.

```
<TransferTableJob sourceTable = "sourceTab" targetTable="targetTab" identicalColumns="true">
	<variable name="varName" type="String" value="test"/>
</TransferTableJob>
```

To reference a variable in SQL use ```${{varName}}```.
To reference a variable in an DSL-expression just use ```varName```.
Valid types are String, DateTime, Boolean, Int64 and Double.
Type conversions in expressions is done mostly implicit (.NET rules) but can be achieved with function calls explicitly too.
Type conversions in ADO.NET SQL have to be done by yourself.

Declaring and (re)setting the value can be done in 3 ways
- Constant – value attribute
  ``` 
  <variable name="`varName" type="String" value="test"/> 
  ```

- SELECT expression
  - Attribute variable/@selectContext defines where to execute the sql query – Target or Source
  - Attribute variable/@selectStmt sets the sql statement
  ```
  <variable name="varName" type="DateTime" selectContext="Source" selectStmt="SELECT Max(datum) from someSourceTab"/>
  <variable name="varName2" type="DateTime" selectContext="Target" selectStmt="SELECT Max(datum) from targetTab where col = ${{varName}} "/>
  ```
- Expression
  - Uses the base DSL itsself to define the value of the variable with the xml attribute variable/@expression
  - referenced variables are substituted as the known type, i.e. a string var with value test will bei substituted as 'test'
  This example assumes that v1 and v2 are existing variables:
  ```
  <variable name="varName" type="String" expression="v1 + ' ' + v2"/>
  ``` 
  

If you define the same variable name multiple times, it is overwriten on each occurence with the new calculated value.
```
<variable name="v1" type="String" value="test" />
<variable name="v2" type="Int64" value="125" />
<variable name="v1" type="String" expression = "v1 + ' ' + v2"/>
```
The result of v1 is 'test 125'.

## Literals

Strings are separated with single quotes '. Escaping is allowed like \' \n \r \t \\ (like C#). 
```'test with \' in it'```

Numbers are written without any braces/just plain.
```12```

Decimals are separated with . .
```12.48```

Boolean literals are plain “true” and “false” without quotes.
```true``` or ```false```

Null literal is plain „null“ without quotes.
```null```

## Operators and relations

All operators and relations in dataTransfer DSLs are binary operators. This means there is always a value before and a value after the operator like

```value operator value```

Unary operators are implemented as functions, like not.


| Type          | Operator  | Usage       | Description |
| ------------- | ----- | -------------|----------------|
| relation | ```<``` | ```2 < 3``` | less |
| relation | ```<=``` | ```2 <= 3``` | less equal |
| relation | ```>``` | ```2 > 3``` | greater |
| relation | ```>``` | ```2 >= 3``` | greater equal |
| relation | ```=``` | ```2 = 3``` | equal <br/> ```=``` has the same meaning like ```==``` |
| relation | ```==``` | ```2 == 3``` | equal <br> ```==``` has the same meaning like ```=```|
| relation | ```!=``` | ```2 != 3``` | unequal <br> ```!=``` has the same meaning like ```<>``` |
| relation | ```<>``` | ```2 != 3``` | unequal <br> ```<>``` has the same meaning like ```!=``` |
| arithmetic operator | ```+``` | ```2 + 3``` | plus |
| arithmetic operator | ```-``` | ```2 - 3``` | minus |
| arithmetic operator| ```*``` | ```2 * 3``` | multiplication |
| arithmetic operator| ```/``` | ```2 / 3``` | division |
| arithmetic operator| ```%``` | ```2 % 3``` | modulo |
| bitwise operator| ```&``` | ```2 & 3``` | bitwise and of bits in numbers |
| bitwise operator| ```|``` | ```2 | 3``` | bitwise or of bits in numbers |
| string operator| ```+``` | ```'2' + '3'``` | string concatenation |
| logic operator| ```&&``` | ```true && false``` | logic and <br/> ```&&``` has the same meaning like ```and``` |
| logic operator| ```||``` | ```true || false``` | logic or <br/> ```||``` has the same meaning like ```or``` |
| logic operator| ```and``` | ```true and false``` | logic and <br/>  ```and``` has the same meaning like ```&&```|
| logic operator| ```or``` | ```true or false``` | logic or <br/> ```or``` has the same meaning like ```||```|

## Functions

The DSLs are functional languages. Functions are called with name and parameters. Example
```Funtcion_name( param1, param2, …, param n)```
The parameter count is dynamic in some cases.
The language is case insensitive. It´s not important if function names are written upper- or lowercase.
The following functions are valid for all DSL variants.

Additional functions could be defined for specific providers like LDAP. These are explained in the appropriate [tutorial section](DataSourceHelpTutorials.md).

### Math functions

| Name | Parameter | Description |
|------|-----------|-------------|
|Sin|Number X<br/>Returns Number|Calculates sine of x|
|Cos|Number X<br/>Returns Number|Calculates cosine of x|
|Tan|Number X<br/>Returns Number|Calculates tangent of x|
|Abs|Number X<br/>Returns Number|Calculates absolute of x|
|Pi|N/A<br/>Returns Number|PI|
|Ceiling|Number X<br/>Returns Number|Rounds the number x up to the next integer|
|Floor|Number X<br/>Returns Number|Rounds the number x down to the next integer|
|Round / rnd|Number X<br/>[integer precision]<br/>Returns Number|Rounds the number x to the next nearest integer.<br/>If precision is set, the rounding is calculated on the n´th decimal position|
|Max|Number X<br/>Number Y<br/>Returns Number|Returns the maximum of [X] and [Y]|
|Min|Number X<br/>Number Y<br/>Returns Number|Returns the minimum of [X] and [Y]|

### String functions

| Name | Parameter | Description |
|------|-----------|-------------|
|ToUpper|String Input<br/>Returns String|Converts all lowercase letters of [input] to uppercase|
|ToLower|String Input<br/>Returns String|Converts all uppercase letters of [input] to lowercase|
|IndexOf|String Input<br/>String SearchString<br/>Returns Number|Searches in string [input] for occurence of [SearchString] and returns the index position.<br/>If the string isn´t found the function returns -1|
|Replace|String Input<br/>String SearchString<br/>String ReplaceString<br/>Returns String|Search and replaces all occurences of [SearchString] in [input] with the replacement string [ReplaceString].|
|Substring|String Input<br/>Number startIndex<br/>[Number Length]<br/>Returns String| Returns a substring of [input]. This is determined from the the start position [startIndex] to the end or if given [Length] in the given [Length] from [startindex].|
|strContains / contains|String Input<br/>String SearchString<br/>Returns Bool|Checks if [input] occurs in string [SearchString].<br/>If so, returns true, else false.|
|strLeft / left|String Input<br/>String SearchString<br/>Returns String|Searchs from the left for the first occurrence of [SearchString] in [input] and returns the substring before the found occurence. If [SearchString] is not found the return is an empty string.|
|strRight / right|String Input<br/>String SearchString<br/>Returns String|Searchs from the left for the first occurrence of [SearchString] in [input] and returns the substring after the found occurence. If [SearchString] is not found the return is an empty string.|
|strMid / Mid|String Input<br/>String SearchStringStart<br/>String SearchStringEnd<br/>Returns String|Searchs from the left for the first occurrence of [SearchStringStart] in [Input]. Then it searches from there for the first occurrence of [SearchStringEnde]. The substring between both findings is returned.<br/>If [SearchStringStart] or [SearchStringEnd] is not found the return is an empty string.|
|startsWith|String Input<br/>String SearchString<br/>Returns Bool|Checks if [input] starts with [SearchString]. If so, returns true, else false.|
|endsWith|String Input<br/>String SearchString<br/>Returns Bool|Checks if [input] ends with [SearchString]. If so, returns true, else false|

### Date functions

| Name | Parameter | Description |
|------|-----------|-------------|
|Date|Number Ticks<br/>OR<br/>Number year<br/>Number month<br/>Number day<br/>[Number hour<br/>Number minute<br/>Number second]<br/>Returns Date|Creates a date from the count of [Ticks] (see .NET documentation for DateTime)<br/>OR<br/>Creates a date composed of the date part parameters [year] [month] [day].<br/>[hour], [minute] and [second] are optional and can be used to set the time as well.|
|AdjustSeconds|Date Input<br/>Number count<br/>Returns Date|Adds [count] seconds to the date returns the calculated date. Negative [count] is subtracted.|
|Adjustminutes|Date Input<br/>Number count<br/>Returns Date|Adds [count] minutes to the date returns the calculated date.<br/>Negative [count] is subtracted.|
|AdjustHours|Date Input<br/>Number count<br/>Returns Date|Adds [count] hours to the date returns the calculated date.<br/>Negative [count] is subtracted.|
|AdjustDays|Date Input<br/>Number count<br/>Returns Date|Adds [count] days to the date returns|
|AdjustMonths|Date Input<br/>Number count<br/>Returns Date|Adds [count] months to the date returns the calculated date.<br/>Negative [count] is subtracted.|
|AdjustYears|Date Input<br/>Number count<br/>Returns Date|Adds [count] years to the date returns the calculated date.<br/>Negative [count] is subtracted.|
|Second|Date Input<br/>Returns Number|Returns the second date time part of [input]|
|Minute|Date Input<br/>Returns Number|Returns the minute date time part of [input]|
|Hour|Date Input<br/>Returns Number|Returns the hour date time part of [input]|
|Day|Date Input<br/>Returns Number|Returns the day date time part of [input]|
|Month|Date Input<br/>Returns Number|Returns the month date time part of [input]|
|Year|Date Input<br/>Returns Number|Returns the year date time part of [input]|

### Conversion functions

| Name | Parameter | Description |
|------|-----------|-------------|
|cstr|Type neutral [input]<br/>Returns String|Converts [input] to string and returns it.|
|Cbool|Type neutral [input]<br/>Returns Bool|Converts [input] to boolean and returns it. If not possible an error is returned.|
|Cint|Type neutral [input]<br/>Returns Ganznumber|Converts [input] to integer and returns it. If not possible an error is returned.|
|Cdbl|Type neutral [input]<br/>Returns Number|Converts [input] to double/number and returns it. If not possible an error is returned.|
|Cdate|Type neutral [input]<br/>Returns Date|Converts [input] to date and returns it. If not possible an error is returned.|
|cChar|Type neutral [input]<br/>Returns Zeichen|Converts [input] to character|

### Logic functions

| Name | Parameter | Description |
|------|-----------|-------------|
|If / iif,/ case / casewhen|Bool Condition1<br/>Type neutral Result1<br/>[Bool Condition 2-n<br/>Type neutral Result 2-n]<br/>Type neutral ElseResult<br/>Returns Type neutral|Checks if [condition1] is true and if so, returns [Result1].<br/>Afterwards check the following conditions in the order of occurrence and return the result for the first true case/if. If no condition is true, return [ElseResult].|
|Nvl|Type neutral [input]<br/>Type neutral [ElseResult]<br/>Returns Type neutral|Checks if [input] is null. If so, returns [ElseResult], otherwise return [input]|
|Not|Bool Input<br/>Returns bool|Changes the boolean value of [input] from true to false or reverse and returns it.|

## SQL Parser for non SQL

Custom SQL allows SQL syntax on non-SQL data sources which do not support SQL like CSV, XML or JSON. 

### Syntax

The base syntax is the same as in original SQL:

``` 
SELECT [columns|expressions]
  FROM [Remote origin] as [tab identifier] [inner join [Remote origin] as [tab identifier] on [bool-expression]]*
 WHERE [bool-expression]
``` 

All expressions are using the [DSL language](DSL.md#Domain_specific_language_DSL).
The syntax of [remote origins](DSL.md#defining-origins-and-remote-request) is described [here](DSL.md#defining-origins-and-remote-request).

Special language characteristics are that
- you have to name all tables and calculated columns with a name/identifier.
  Names of columns or “tables” are named with “AS Name”.
  ```SELECT 1 as Column FROM Table as T```
- Inner joins accept only = as condition with “=” are accepted.
  More complex comparisions are possible in the where clause
  ```Select T1.Key FROM Tab1 as T1 inner join Tab2 T2 on T1.Key = T2.Key```
- If using multiple tables/[remote origins](DSL.md#defining-origins-and-remote-request), every column has to be specified full qualified. Every column has to be refrenced by [remoteOrigin alias].[column name]
-  Variables can be inserted with the data binding expression ${{Varname}}
  ```SELECT 3 + ${{NumberVar}} as Calc from Tab AS T1```

### Defining origins and remote request

The FROM part of non-SQL SQL expressions is called a remote request. the basic syntax is
```
[protocol]://[resource location]

i.e.
file://C:\temp\test.csv
http://test.com/myResource.csv
```

If no protocol is set, dataTransfer assumes file:// .

Additional to these URIs, dataTransfer uses an own parameter syntax for each remote origin. These look like this
```
[key]=[value]:::[key]=[value]:::[...]:::[protocol]://[resource location]
i.e.
http-method=get:::http-header=Accept:application/csv:::http://servername:port/query?db=test
```

These parameters depend on the used protocol and data provider.
If a parameter name is listed multiple times, it is handled as list value. In the previous examle http-header can be used as often as needed.
Provider dependent parameters are described in the appropriate [tutorial section](DataSourceHelpTutorials.md).
Protocol dependent parameters are described here.

#### file

if no protocol is specified file is assumed.
Authentication context is the user executing dataTransfer.
It is allowed to use \ or / as directory separator. / are converted internaly to \. 
Drive letters are transformed to [letter]$.

The following parameters are allowed.

| Name | Description |
|------|-------------|
|file-server|The server where the share is located - Default localhost|

Because of these many interpretations this 
```C:\temp/test.csv```
is transformed to
```\\localhost\C$\temp\test.csv```

#### http and https

Authentication context is the user executing dataTransfer.
The following parameters are allowed.

| Name | Description |
|------|-------------|
|http-method|The http method to use for this web request. Allowed values are GET, POST, PUT, DELETE - Default GET|
|http-postdata| Data which should be sent in a POST, PUT or DELETE|
|http-timeout|Timeout of the web request in seconds - default 120|
|http-header|Multiple usage possible, once for each http header. Header named in format Headername:Headerwert|
|http-SecProtType|Security protocol version for https i.e. Tls12. Valid are the enum values of System.Net.SecurityProtocolType|

#### ldap and ldaps

No additional parameters for ldap:// or ldaps:// are allowed currently.
Authentication context is the user executing dataTransfer.

[Back to index](docIndex.md)