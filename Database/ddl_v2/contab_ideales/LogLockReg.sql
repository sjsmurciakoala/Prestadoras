USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[LogLockReg]    Script Date: 10/12/2025 13:58:26 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[LogLockReg](
	[KeyRegLock] [int] IDENTITY(1,1) NOT NULL,
	[IDStation] [int] NOT NULL,
	[ID_Entity] [varchar](10) NOT NULL,
	[TableName] [varchar](20) NOT NULL,
	[siPeriod] [smallint] NOT NULL,
	[IDGeneral] [varchar](25) NOT NULL,
	[dtDate] [datetime] NOT NULL,
	[Status] [smallint] NOT NULL,
 CONSTRAINT [LogLockReg0] PRIMARY KEY CLUSTERED 
(
	[KeyRegLock] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[LogLockReg] ADD  DEFAULT ((0)) FOR [IDStation]
GO

ALTER TABLE [dbo].[LogLockReg] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[LogLockReg] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[LogLockReg] ADD  DEFAULT ((0)) FOR [Status]
GO

ALTER TABLE [dbo].[LogLockReg]  WITH CHECK ADD CHECK  ((len([ID_Entity])>(0)))
GO

ALTER TABLE [dbo].[LogLockReg]  WITH CHECK ADD CHECK  (([siPeriod]>=(1980) AND [siPeriod]<=(2025)))
GO

ALTER TABLE [dbo].[LogLockReg]  WITH CHECK ADD CHECK  (([Status]=(1) OR [Status]=(0) OR [Status]=(-1)))
GO


