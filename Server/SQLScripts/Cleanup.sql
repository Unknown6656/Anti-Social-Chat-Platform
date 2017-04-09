DELETE FROM [UserAuthentifications]
WHERE [ID] NOT IN (SELECT u.[ID]
				   FROM [Users] u)