-- PARAMS:	{0} = id
--			{1} = salt
--			{2} = name
--			{3} = status
--			{4} = admin
--			{5} = blocked

INSERT INTO [Users] (
	[ID],
	[Name],
	[Status],
	[IsAdmin],
	[IsBlocked],
	[UUID],
	[MemberSince]
) VALUES (
	{0},
	'{2}',
	'{3}',
	{4},
	{5},
	NEWID(),
	GETDATE()
);
INSERT INTO [UserAuthentifications] (
	[ID],
	[Hash],
	[Salt],
	[Session],
	[LastIP],
	[LastLogin],
	[LastUserAgent],
	[LastLocation]
) VALUES (
	{0},
	'',
	'{1}',
	NULL,
	NULL,
	NULL,
	NULL,
	NULL
)