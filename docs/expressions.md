# Expressions & Custom Formula

Expressions in the Report Designer allow you to perform calculations and manipulate data within your reports.

Custom Formula is a feature where user can perform very complex operations on report. Even user can use another Custom Formula for new Custom Formula fields. 

# Expressions

Below are supported expressions that you can use in your report fields:

- **Special Fields**:
   - **Page Number**
   - **Total Pages**
   - **Today**
   - **Now**
   - **Row Number**
- **Functions**:
   - **Sum**
   - **Count**
   - **Average**
   - **Min**
   - **Max**
   - **If condition**
   - **ISNULL**
   - **Format**
   - **Round**
   - **Upper**
   - **Lower**
   - **Trim**
   - **Len**
   - **Substring**
   - **Concat**

## PAGENUMBER()

Returns the current page number

### Syntax

PAGENUMBER()

### Example

PAGENUMBER()

### Result

27


## TOTALPAGES()

Returns total number of pages in the report.

### Syntax

TOTALPAGES()

### Example

TOTALPAGES()

### Result

387



## TODAY()

Returns the current date.

### Syntax

TODAY()

### Example

TODAY()

### Result

7/2/2026 12:00:00 AM



## NOW()

Returns the current date and time.

### Syntax

NOW()

### Example

NOW()

### Result

7/2/2026 2:53:27 PM



## ROWNUMBER()

Returns the current row number.

### Syntax

ROWNUMBER()

### Example

ROWNUMBER()

### Result

5


## SUM()

Calculates the total value.

### Syntax

SUM(expression)

### Example

SUM(Order.Amount)

### Result

1500


## COUNT()

Count the total in field.

### Syntax

COUNT(expression)

### Example

COUNT(Order.CustomerName)

### Result

54


## AVERAGE()

Calculates the average value.

### Syntax

AVERAGE(expression)

### Example

AVERAGE(Order.Amount)

### Result

300


## MIN()

Calculates the minimum value.

### Syntax

MIN(expression)

### Example

MIN(Order.Amount)

### Result

35


## MAX()

Calculates the maximum value.

### Syntax

MAX(expression)

### Example

MAX(Order.Amount)

### Result

513


## IIF()

Check field against the condition and return the value based on the condition.

### Syntax

IIF(condition, true_value, false_value)

### Example

IIF(Order.Amount > 100, "High", "Low")

### Result

"Low"


## ISNULL()

Check if a field is null and return a specified value.

### Syntax

ISNULL(expression, replacement_value)

### Example

ISNULL(Order.CustomerName, "Unknown")

### Result

"Unknown"


## FORMAT()

Apply formatting to a field value.

### Syntax

FORMAT(expression, format_string)

### Example

FORMAT(Order.Amount, "C")

### Result

"$1,500.00"


## ROUND()

Apply rounding to a field value.

### Syntax

ROUND(expression, decimal_places)

### Example

ROUND(Order.Amount, 2)

### Result

287.62


## UPPER()

Convert a string to uppercase.

### Syntax

UPPER(expression)

### Example

UPPER(Order.CustomerName)

### Result

"JOHN DOE"


## LOWER()

Convert a string to lowercase.

### Syntax

LOWER(expression)

### Example

LOWER(Order.CustomerName)

### Result

"john doe"


## LOWER()

Convert a string to lowercase.

### Syntax

LOWER(expression)

### Example

LOWER(Order.CustomerName)

### Result

"john doe"


## TRIM()

Remove leading and trailing whitespace from a string.

### Syntax

TRIM(expression)

### Example

TRIM(Order.CustomerName)

### Result

"Trimmed Text" (if we have " Trimmed Text   ")


## LEN()

Returns the length of a string.

### Syntax

LEN(expression)

### Example

LEN(Order.CustomerName)

### Result

17


## LEN()

Returns the length of a string.

### Syntax

LEN(expression)

### Example

LEN(Order.CustomerName)

### Result

17


## SUBSTRING()

Extract a specific portion or segment of characters from an existing string

### Syntax

SUBSTRING(expression, start index, length)

### Example

SUBSTRING(Order.CustomerName, 1, 2)

### Result

"oh"  (If we have "John Joe")



## CONCAT()

Concatenate the two or more than two string

### Syntax

CONCAT(expression1, expression2, ...  expression N)

### Example

CONCAT(Order.OrderId, Order.CustomerName)

### Result

"5John Joe"


# Custom Formula

You can find this field in the "Charts & Custom Formula" section in the Left Panel. Once you drag and drop this field to canvas, you will be enabled with Custom Formula Editor in the Right Side Panel. 

- A textarea for typing the formula expression
- Operator buttons (+, -, *, /, (, ), .)
- Field insert buttons — schema fields (inserts Fields.FieldName)
- Previous custom fields — references other custom formulas via Custom.FieldName
- Special values — RunningTotal(), PageNumber(), RowNumber(), Today()
- Function buttons — SUM, COUNT, AVG, MIN, MAX, IIF, ISNULL, ROUND
- Options — IsRunningTotal checkbox; when checked, a "Reset on group" field appear

In the textarea, you can enter formula like,

```
(Fields.Amount + Fields.Commission) / Fields.TotalAmount
```

If you want to use Custom Formula value on the another custom formula, you may use like,

```
custom_b1384ec9 + Fields.AdditionAmt
```