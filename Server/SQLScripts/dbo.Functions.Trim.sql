﻿
CREATE FUNCTION [dbo].TRIM (
	@str AS VARCHAR(MAX)
)
RETURNS VARCHAR(MAX)
BEGIN
	RETURN LTRIM(RTRIM(@str))
END
