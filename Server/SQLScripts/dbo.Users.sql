CREATE TABLE [dbo].[Users] (
    [ID]          BIGINT           DEFAULT -1 NOT NULL,
    [Name]        VARCHAR (50)     DEFAULT '' NOT NULL,
    [Status]      VARCHAR (256)    DEFAULT '' NULL,
    [IsAdmin]     BIT              DEFAULT 0 NOT NULL,
    [IsBlocked]   BIT              DEFAULT 0 NOT NULL,
    [UUID]        UNIQUEIDENTIFIER NOT NULL,
    [MemberSince] DATETIME         DEFAULT getdate() NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
