USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01DBAudit]    Script Date: 10/12/2025 13:35:03 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01DBAudit](
	[siPeriod] [smallint] NOT NULL,
	[TypeTrans] [tinyint] NOT NULL,
	[TableName] [varchar](20) NOT NULL,
	[IDGeneral] [varchar](25) NOT NULL,
	[NLine] [int] IDENTITY(1,1) NOT NULL,
	[DatFields] [varchar](100) NOT NULL,
	[DatBefore] [varchar](200) NULL,
	[DatAfter] [varchar](200) NULL,
	[Event] [tinyint] NOT NULL,
	[IDUser] [varchar](10) NOT NULL,
	[dtDate] [datetime] NOT NULL,
 CONSTRAINT [C01DBAudit0] PRIMARY KEY CLUSTERED 
(
	[siPeriod] ASC,
	[TypeTrans] ASC,
	[TableName] ASC,
	[IDGeneral] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01DBAudit] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01DBAudit] ADD  DEFAULT ((0)) FOR [TypeTrans]
GO

ALTER TABLE [dbo].[C01DBAudit] ADD  DEFAULT ((0)) FOR [Event]
GO

ALTER TABLE [dbo].[C01DBAudit] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[C01DBAudit]  WITH CHECK ADD  CONSTRAINT [C01DBAuditFK01] FOREIGN KEY([IDUser])
REFERENCES [dbo].[CGUSRS] ([CodUsua])
GO

ALTER TABLE [dbo].[C01DBAudit] CHECK CONSTRAINT [C01DBAuditFK01]
GO

ALTER TABLE [dbo].[C01DBAudit]  WITH CHECK ADD CHECK  (([Event]>=(0) AND [Event]<=(2)))
GO

ALTER TABLE [dbo].[C01DBAudit]  WITH CHECK ADD CHECK  ((len([IDUser])<>(0)))
GO

ALTER TABLE [dbo].[C01DBAudit]  WITH CHECK ADD CHECK  (([TypeTrans]>=(0) AND [TypeTrans]<=(5)))
GO


