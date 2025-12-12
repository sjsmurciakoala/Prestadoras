USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01TransFixedItem]    Script Date: 10/12/2025 13:49:01 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01TransFixedItem](
	[KeyTransFix] [int] IDENTITY(1,1) NOT NULL,
	[KeyFix] [int] NOT NULL,
	[NLine] [smallint] NOT NULL,
	[siType] [tinyint] NOT NULL,
	[ID_Account] [varchar](24) NOT NULL,
	[ID_Fix] [varchar](15) NOT NULL,
	[ID_Cost] [varchar](8) NULL,
	[dtDate] [datetime] NOT NULL,
	[Amount] [decimal](28, 2) NOT NULL,
	[Factor] [decimal](28, 9) NOT NULL,
	[Units] [decimal](28, 2) NOT NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [C01TransFixedItem0] PRIMARY KEY CLUSTERED 
(
	[KeyTransFix] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01TransFixedItem] ADD  DEFAULT ((0)) FOR [NLine]
GO

ALTER TABLE [dbo].[C01TransFixedItem] ADD  DEFAULT ((0)) FOR [siType]
GO

ALTER TABLE [dbo].[C01TransFixedItem] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[C01TransFixedItem] ADD  DEFAULT ((0)) FOR [Amount]
GO

ALTER TABLE [dbo].[C01TransFixedItem] ADD  DEFAULT ((0)) FOR [Factor]
GO

ALTER TABLE [dbo].[C01TransFixedItem] ADD  DEFAULT ((0)) FOR [Units]
GO

ALTER TABLE [dbo].[C01TransFixedItem] ADD  DEFAULT ((0)) FOR [boStatus]
GO

ALTER TABLE [dbo].[C01TransFixedItem]  WITH CHECK ADD  CONSTRAINT [C01TransFixedItemFK01] FOREIGN KEY([KeyFix])
REFERENCES [dbo].[C01TransFixed] ([KeyFix])
GO

ALTER TABLE [dbo].[C01TransFixedItem] CHECK CONSTRAINT [C01TransFixedItemFK01]
GO

ALTER TABLE [dbo].[C01TransFixedItem]  WITH CHECK ADD  CONSTRAINT [C01TransFixedItemFK02] FOREIGN KEY([ID_Fix])
REFERENCES [dbo].[C01FixedAssest] ([ID_Fix])
GO

ALTER TABLE [dbo].[C01TransFixedItem] CHECK CONSTRAINT [C01TransFixedItemFK02]
GO

ALTER TABLE [dbo].[C01TransFixedItem]  WITH CHECK ADD  CONSTRAINT [C01TransFixedItemFK03] FOREIGN KEY([ID_Account])
REFERENCES [dbo].[C01Account] ([ID_Account])
GO

ALTER TABLE [dbo].[C01TransFixedItem] CHECK CONSTRAINT [C01TransFixedItemFK03]
GO

ALTER TABLE [dbo].[C01TransFixedItem]  WITH CHECK ADD CHECK  (([siType]>=(0) AND [siType]<=(2)))
GO


