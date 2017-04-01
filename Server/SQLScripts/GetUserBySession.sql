-- PARAMS:	{0} = session

DECLARE @id BIGINT
DECLARE @time DATETIME

SET @time = DATEADD(MINUTE, -5, GETDATE())
SET @id = (
	SELECT TOP(1) [ID]
	FROM [UserAuthentifications]
	WHERE UPPER([Session]) = '{0}'
	AND [LastLogin] > @time
)

SELECT TOP(1) *
FROM [Users]
WHERE [ID] = @id
AND [IsBlocked] = 0
