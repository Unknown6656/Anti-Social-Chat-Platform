-- PARAMS: {0} = SEARCH STRING

DECLARE @name VARCHAR(50)
SET @name = [dbo].TRIM('{0}')
SELECT TOP(20) users.*
FROM (SELECT TOP(20)
			*,
			SOUNDEX([Name]) as Soundex,
			-1 as Difference
	  FROM [Users]
	  WHERE ([dbo].TRIM([Name]) LIKE ('%' + @name + '%'))
	  -- OR (@name LIKE ('%' + [dbo].TRIM([Name]) + '%'))
	  UNION
	  SELECT TOP(5)
			*,
			SOUNDEX([Name]) as Soundex,
			DIFFERENCE(@name, [Name]) as Difference
	  FROM [Users]
	  WHERE DIFFERENCE(@name, [Name]) < 2
) AS users
