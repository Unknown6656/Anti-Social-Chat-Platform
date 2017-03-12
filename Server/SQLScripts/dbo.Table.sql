CREATE TABLE [dbo].[UserAuthentifications]
(
    [ID]        BIGINT              DEFAULT -1  NOT NULL,
    [Hash]      VARCHAR(128)        DEFAULT '' NOT NULL,
    [Salt]      VARCHAR(128)        DEFAULT '' NOT NULL,
    [Session]   VARCHAR(128)        NULL,
    [LastIP]    VARCHAR(50)         NULL,
    [LastLogin] DATETIME            NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC)
)
