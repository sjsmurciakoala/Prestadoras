USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01Periods]    Script Date: 10/12/2025 13:42:02 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01Periods](
	[siPeriod] [smallint] NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[dtStart] [datetime] NOT NULL,
	[dtEnd] [datetime] NOT NULL,
	[Status] [smallint] NOT NULL,
	[SysMonth] [varchar](14) NOT NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [C01Periods0] PRIMARY KEY CLUSTERED 
(
	[siPeriod] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01Periods] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01Periods] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[C01Periods] ADD  DEFAULT (getdate()) FOR [dtStart]
GO

ALTER TABLE [dbo].[C01Periods] ADD  DEFAULT (getdate()) FOR [dtEnd]
GO

ALTER TABLE [dbo].[C01Periods] ADD  DEFAULT ((0)) FOR [Status]
GO

ALTER TABLE [dbo].[C01Periods] ADD  DEFAULT ('00000000000000') FOR [SysMonth]
GO

ALTER TABLE [dbo].[C01Periods] ADD  DEFAULT ((1)) FOR [boStatus]
GO

ALTER TABLE [dbo].[C01Periods]  WITH CHECK ADD CHECK  (([Status]>=(0) AND [Status]<=(2)))
GO


