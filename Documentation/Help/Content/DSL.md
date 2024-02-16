[Back to index](docIndex.md)
# Domain specific language DSL

The dataTransfer is able to define and use variables and query non ADO.NET data sources. 
Because non ADO.NET data sources mostly don´t understand SQL the dataTransfer uses an own [SQL Parser](DSL.md#SQL-Parser-for-non-SQL) and expression language for [variables](DSL.md#Variables), where parts and columns.
Variables can also be used in standard SQL statement of ADO.NET data sources.

There are several variants of expression languages build in, depending on the custom data provider. For variable initialization without provider context, the most basic variant is used.
The basic syntax of all DSLs is described here. Special functions are described at the documentation for the [concrete custom provider](DataSourceHelpTutorials.md#non-ADO/custom-transfers).

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
  - Uses the DSL itsself to define the value of the variable with the attribute variable/@expression
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

All operators and relations in dataTransfer DSL are binary operators. This means there is only a value before and a value after the operator like

```value operator value```

Unary operators are implemented as functions, like not.


| Type          | Operator  | Usage       | Description |
| ------------- | ----- | -------------|----------------|
| relation | < | 2 < 3 | less |
| relation | <= | 2 <= 3 | less equal |
| relation | > | 2 > 3 | greater |
| relation | > | 2 >= 3 | greater equal |
| relation | = | 2 = 3 | equal <br/> = has the same meaning like == |
| relation | == | 2 == 3 | equal <br> == has the same meaning like =|
| relation | != | 2 != 3 | unequal <br> != has the same meaning like <> |
| relation | <> | 2 != 3 | unequal <br> <> has the same meaning like != |
| relation | < | 2 < 3 | less |
| relation | < | 2 < 3 | less |


## Functions

## SQL Parser for non SQL

### Syntax

### Defining origins and remote request

[Back to index](docIndex.md)