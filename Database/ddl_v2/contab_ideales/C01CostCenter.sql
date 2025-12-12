USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01CostCenter]    Script Date: 10/12/2025 13:33:19 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01CostCenter](
	[TypeTrans] [tinyint] NOT NULL,
	[ID_Cost] [varchar](8) NOT NULL,
	[KeyCost] [int] IDENTITY(1,1) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[ID_Parent] [varchar](24) NULL,
	[boMovement] [bit] NOT NULL,
	[dtStart] [datetime] NOT NULL,
	[dtEnd] [datetime] NOT NULL,
	[Note] [text] NULL,
	[boPeriod] [bit] NOT NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [C01CostCenter0] PRIMARY KEY CLUSTERED 
(
	[ID_Cost] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01CostCenter] ADD  DEFAULT ((0)) FOR [TypeTrans]
GO

ALTER TABLE [dbo].[C01CostCenter] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[C01CostCenter] ADD  DEFAULT ((0)) FOR [boMovement]
GO

ALTER TABLE [dbo].[C01CostCenter] ADD  DEFAULT (getdate()) FOR [dtStart]
GO

ALTER TABLE [dbo].[C01CostCenter] ADD  DEFAULT (getdate()) FOR [dtEnd]
GO

ALTER TABLE [dbo].[C01CostCenter] ADD  DEFAULT ((0)) FOR [boPeriod]
GO

ALTER TABLE [dbo].[C01CostCenter] ADD  DEFAULT ((0)) FOR [boStatus]
GO

ALTER TABLE [dbo].[C01CostCenter]  WITH CHECK ADD CHECK  (([TypeTrans]>=(0) AND [TypeTrans]<=(5)))
GO


