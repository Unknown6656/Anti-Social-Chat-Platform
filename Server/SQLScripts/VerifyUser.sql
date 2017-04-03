-- PARAMS:	{0} = id
--			{1} = hash

SELECT 1
FROM [UserAuthentifications]
WHERE [ID] = {0}
AND (UPPER([Hash]) = '{1}' OR
	(LOWER('{1}') = 'null') AND (([Hash] = NULL) OR ([Hash] = '')))
AND (SELECT COUNT(0)
        FROM [Users]
        WHERE [ID] = {0}
        AND [IsBlocked] = 0
    ) = 1