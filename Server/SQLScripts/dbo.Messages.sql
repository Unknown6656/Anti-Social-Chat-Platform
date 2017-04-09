CREATE TABLE [dbo].[Messages] (
    [ID]				BIGINT        DEFAULT -1 NOT NULL,
    [SenderID]			BIGINT        DEFAULT -1 NOT NULL,
    [SendDate]			DATETIME      DEFAULT 0 NOT NULL,
    [IsSecured]			BIT           DEFAULT 0 NOT NULL,
    [IsDelivered]		BIT           DEFAULT 0 NOT NULL,
    [IsRead]			BIT           DEFAULT 0 NOT NULL,
    [ReadDate]			DATETIME      DEFAULT 0 NOT NULL,
    [SenderIP]			VARCHAR (50)  DEFAULT '' NULL,
    [SenderUA]			VARCHAR (MAX) DEFAULT '' NULL,
    [SenderLocation]    VARCHAR (256) DEFAULT '' NULL,
    [Content]			VARCHAR (MAX) DEFAULT '' NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
);
