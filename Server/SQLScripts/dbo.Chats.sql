CREATE TABLE [dbo].[Chats] (
    [ID]           BIGINT       DEFAULT -1 NOT NULL,
    [Name]         VARCHAR (50) DEFAULT '' NOT NULL,
    [CreationDate] DATETIME     DEFAULT 0 NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
