-- PARAMS:	{0} = new hash
--			{1} = id

UPDATE [UserAuthentifications]
SET [Hash] = '{0}'
WHERE [ID] = {1}
AND (SELECT COUNT(0)
     FROM [Users]
     WHERE [ID] = {1}
     AND [IsBlocked] = 0
    ) = 1