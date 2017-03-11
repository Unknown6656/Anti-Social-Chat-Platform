CREATE TABLE [dbo].[ChatMembers] (
    [ChatID]      BIGINT   DEFAULT -1 NOT NULL,
    [UserID]      BIGINT   DEFAULT -1 NOT NULL,
    [MemberSince] DATETIME DEFAULT 0 NOT NULL
);
