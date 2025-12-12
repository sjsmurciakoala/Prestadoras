USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[LogStation]    Script Date: 10/12/2025 14:02:50 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[LogStation](
	[IDStation] [int] NOT NULL,
	[ID_Entity] [varchar](10) NOT NULL,
	[IDUser] [varchar](10) NOT NULL,
	[IDLog] [int] NOT NULL,
	[dtDate] [datetime] NOT NULL,
	[Status] [smallint] NOT NULL,
	[StationName] [varchar](50) NOT NULL,
 CONSTRAINT [LogStation0] PRIMARY KEY CLUSTERED 
(
	[IDStation] ASC,
	[ID_Entity] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[LogStation] ADD  DEFAULT ((0)) FOR [IDStation]
GO

ALTER TABLE [dbo].[LogStation] ADD  DEFAULT ((0)) FOR [IDLog]
GO

ALTER TABLE [dbo].[LogStation] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[LogStation] ADD  DEFAULT ((0)) FOR [Status]
GO

ALTER TABLE [dbo].[LogStation] ADD  DEFAULT (host_name()) FOR [StationName]
GO

ALTER TABLE [dbo].[LogStation]  WITH CHECK ADD CHECK  ((len([ID_Entity])>(0)))
GO

ALTER TABLE [dbo].[LogStation]  WITH CHECK ADD CHECK  ((len([IDUser])>(0)))
GO

ALTER TABLE [dbo].[LogStation]  WITH CHECK ADD CHECK  (([Status]=(1) OR [Status]=(0) OR [Status]=(-1)))
GO


