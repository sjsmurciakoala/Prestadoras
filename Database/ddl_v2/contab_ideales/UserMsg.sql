USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[UserMsg]    Script Date: 10/12/2025 14:10:13 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[UserMsg](
	[NLine] [int] IDENTITY(1,1) NOT NULL,
	[IDUser] [varchar](10) NOT NULL,
	[StationName] [varchar](50) NOT NULL,
	[IDFrom] [varchar](10) NOT NULL,
	[Note] [text] NOT NULL,
	[dtDate] [datetime] NOT NULL,
	[Status] [smallint] NOT NULL,
 CONSTRAINT [UserMsg0] PRIMARY KEY CLUSTERED 
(
	[NLine] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[UserMsg] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[UserMsg] ADD  DEFAULT ((0)) FOR [Status]
GO

ALTER TABLE [dbo].[UserMsg]  WITH CHECK ADD CHECK  ((len([IDFrom])>(0)))
GO

ALTER TABLE [dbo].[UserMsg]  WITH CHECK ADD CHECK  ((len([IDUser])>(0)))
GO

ALTER TABLE [dbo].[UserMsg]  WITH CHECK ADD CHECK  (([Status]=(2) OR [Status]=(1) OR [Status]=(0)))
GO


