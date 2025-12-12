USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01FixedAdd]    Script Date: 10/12/2025 13:37:35 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01FixedAdd](
	[ID_AddFix] [varchar](10) NOT NULL,
	[ID_Fix] [varchar](15) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[dtDate] [datetime] NOT NULL,
	[Qty] [int] NOT NULL,
	[CurrencyRate] [decimal](28, 9) NULL,
	[Amount] [decimal](28, 2) NOT NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [C01FixedAdd0] PRIMARY KEY CLUSTERED 
(
	[ID_AddFix] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01FixedAdd] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[C01FixedAdd] ADD  DEFAULT ((0)) FOR [Qty]
GO

ALTER TABLE [dbo].[C01FixedAdd] ADD  DEFAULT ((0)) FOR [CurrencyRate]
GO

ALTER TABLE [dbo].[C01FixedAdd] ADD  DEFAULT ((0)) FOR [Amount]
GO

ALTER TABLE [dbo].[C01FixedAdd] ADD  DEFAULT ((1)) FOR [boStatus]
GO

ALTER TABLE [dbo].[C01FixedAdd]  WITH CHECK ADD  CONSTRAINT [C01FixedAddFK01] FOREIGN KEY([ID_Fix])
REFERENCES [dbo].[C01FixedAssest] ([ID_Fix])
GO

ALTER TABLE [dbo].[C01FixedAdd] CHECK CONSTRAINT [C01FixedAddFK01]
GO


