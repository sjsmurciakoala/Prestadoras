USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01TransFixed]    Script Date: 10/12/2025 13:48:37 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01TransFixed](
	[KeyFix] [int] IDENTITY(1,1) NOT NULL,
	[siType] [tinyint] NOT NULL,
	[Reference] [varchar](20) NOT NULL,
	[dtDate] [datetime] NOT NULL,
	[Concept] [varchar](50) NOT NULL,
	[Status] [smallint] NOT NULL,
 CONSTRAINT [C01TransFixed0] PRIMARY KEY CLUSTERED 
(
	[KeyFix] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01TransFixed] ADD  DEFAULT ((0)) FOR [siType]
GO

ALTER TABLE [dbo].[C01TransFixed] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[C01TransFixed] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Concept]
GO

ALTER TABLE [dbo].[C01TransFixed] ADD  DEFAULT ((0)) FOR [Status]
GO

ALTER TABLE [dbo].[C01TransFixed]  WITH CHECK ADD CHECK  (([siType]>=(0) AND [siType]<=(2)))
GO


