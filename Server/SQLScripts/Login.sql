-- PARAMS:	{0} = user agent
--			{1} = ip
--			{2} = session
--			{3} = id

UPDATE [UserAuthentifications]
SET [LastUserAgent] = '{0}',
    [LastIP] = '{1}',
    [Session] = '{2}',
    [LastLogin] = getdate()
WHERE [ID] = {3}
AND (SELECT COUNT(0)
     FROM [Users]
     WHERE [ID] = {3}
     AND [IsBlocked] = 0
    ) = 1