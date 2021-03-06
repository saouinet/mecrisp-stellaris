\ si4432_spi.fs from stm32f103rb
\ 18.05.2016
\ Ilya Abdrahimov ilya73@inbox.ru
\ PB13 - spi22_SCK
\ PB14 - spi22_MISO
\ PB15 - spi22_MOSI
\ PB12 - nSEL
\ PB2 - nIRQ

40 buffer: si4432-rxbuf
0 variable si4432_pkt_len	\ Длина принятого пакета
\ 0 variable si4432-pkt-flg	\ Флаг
2 constant SI4432_RF_PACKET_RECEIVED
5 constant SI4432_RF_NO_PACKET
6 constant SI4432_RF_CRC_ERROR
0 variable SI4432_status
GPIOB 4 + constant GPIOB_H
\ Primitives for spi2 bit-banging

1 12 lshift constant si4432-select
1 2 lshift constant si4432-nirq
: si4432-sel   ( -- ) si4432-select 16 lshift gpiob_bsrr ! inline ;
: si4432-unsel ( -- ) si4432-select           gpiob_bsrr ! inline ;

\ : miso ( -- ? ) spi2-miso gpiob_idr bit@ inline ;
: nIRQ? ( -- ? ) si4432-nirq gpiob_idr bit@ 0= inline ;

SPI2 $c + constant SPI2_DR
SPI2 $8 + constant SPI2_SR

: si4432-gpio-init ( -- )
GPIOB_H @ $ffff and GPIOB_H !	\ Очистили старшие разряды

%1001 28 lshift							\ MOSI AF    
%1001 20 lshift or    					\ SCK AF
\ %1001 16 lshift or    					\ nSEL AF
GPIOB_H !
  1 18 lshift gpiob 4 + bic!
  1 16 lshift gpiob 4 + bis!
   si4432-unsel
1 27 lshift GPIOB_H bis!				\ MISO input mode

1 14 lshift RCC_APB1ENR bis!			\ enable SPI2

%001 3 lshift \ /4
1 9 lshift or
1 8 lshift or
1 2 lshift or	\ master
SPI2 h!
1 6 lshift	SPI2 bis!	\ SPI enable
  \ Finished.
;

: >spi2> ( c -- c ) 
 SPI2_DR !
 true begin 1- dup 0= 2 SPI2_SR bit@  or until drop	\ Wait TX finish 
 true begin 1- dup 0= 1 SPI2_SR bit@ or until	drop \ Wait RX finish  
 SPI2_DR @
;
: >spi2 ( c -- ) >spi2> drop ;
: spi2> ( -- c ) 0 >spi2> ;

: si4432-read
spi2>
;

: si4432-write
>spi2
;

: SI4432> ( adr -- n)
si4432-sel
si4432-write
si4432-read
si4432-unsel
;

: >SI4432 ( adr n -- )
 si4432-sel
swap $80 or
si4432-write
si4432-write
 si4432-unsel
;

: si4432-rdintst  ( -- n n1 ) 3 SI4432> 4 SI4432> ;

: SI4432-init
si4432-gpio-init
100 ms
\ read interrupt status
si4432-rdintst 2drop
\ SW reset -> wait for POR interrupt
7 $80 >SI4432
100 ms
\ read interrupt status
3 SI4432> drop
\
7 0 >SI4432
$65 $a1 >SI4432
\ disable all ITs, except 'ichiprdy'
5 0 >SI4432
6 2 >SI4432
si4432-rdintst 2drop
\ set the non-default Si4432 registers
\ set TX Power +11dBm
$6d 0 >SI4432
\ set VCO
$5a $7f >SI4432
$59 $40 >SI4432
\ set the AGC
$6a $b >SI4432
\ set ADC reference voltage to 0.9V
$68 $4 >SI4432
\ set cap. bank
$9 $7f >SI4432
\ reset digital testbus, disable scan test
$51 41 >SI4432	\ ?
\ select nothing to the Analog Testbus
$50 $b >SI4432
\ set frequency
$75 $53 >SI4432
$76 $64 >SI4432
$77 $00 >SI4432
\ disable RX-TX headers
\ $32 0 >SI4432
\ $33 2 >SI4432
$32 $ff >si4432
$33 $42 >si4432
\ Transmit header
$3a [char] s >si4432
$3b [char] e >si4432
$3c [char] r >si4432
$3d [char] 1 >si4432
\ Check header
$3f [char] c >si4432
$40 [char] l >si4432
$41 [char] i >si4432
$42 [char] 1 >si4432

\ set the sync word
$36 $2d >SI4432
$37 $d4 >SI4432
\ set GPIO0 to MCLK
$b $12 >SI4432
$c $15 >SI4432
\ set modem and RF parameters according to the selected DATA rate
\ setup the internal digital modem according the selected RF settings (data rate)
$1c $4 >SI4432
$20 $41 >SI4432
$21 $60 >SI4432
$22 $27 >SI4432
$23 $52 >SI4432
$24 $00 >SI4432
$25 $0a >SI4432
$6e $27 >SI4432
$6f $52 >SI4432
$70 $20 >SI4432
$72 $48 >SI4432
$1d $40 >SI4432
$58 $80 >SI4432
\ enable packet handler & CRC16
$30 $8d >SI4432
$71 $63 >SI4432
\ set preamble length & detection threshold
$34 $4 1 lshift >SI4432
$35 $2 4 lshift >SI4432
$1f $3 >SI4432
\ reset digital testbus, disable scan test
$51 $0 >SI4432
\ select nothing to the Analog Testbus
$50 $b >SI4432
;



: SI4432-Transmit ( adr n -- )
\ set packet length

$3e over >SI4432
over + swap
?do
	$7f i c@ >SI4432 
loop
\ errata
$65 $a1 >SI4432 5 ms
\ enable transmitter
$7 $9 >SI4432 5 ms
\ enable the packet sent interrupt only
$5 $4 >SI4432
\ read interrupt status registers
si4432-rdintst 2drop
\ wait for the packet sent interrupt
true begin 1- dup 0= nIRQ? or until drop
si4432-rdintst drop SI4432_status !
;

: SI4432-Receive
\ errata
$65 $a1 >SI4432 5 ms
\ enable receiver chain
$7 $5 >SI4432 5 ms
\ enable the wanted ITs
$5 $12 >SI4432 \ RX FIFO full & valid packet received
$6 $00 >SI4432
\ read interrupt status registers
\ si4432-rdintst 2drop
;



: SI4432-Packet-Recived ( -- )
		\ check what caused the interrupt 
		\ read out IT status register
		\ si4432-rdintst drop dup SI4432_status ! 
		\ packet received interrupt occurred
		\ 2 and if
			$4b SI4432> 	\ packet lenght
			si4432-rxbuf over over + swap
			?do $7f SI4432> i c! loop
			si4432_pkt_len !
		\ then
;

\ 8 constant EXTI2_IRQn  \ EXTI Line2 Interrupt
\ : si4432-irq
\ si4432-rdintst drop 
\ 2 and 	if		\	Приняли пакет
\			SI4432-Packet-Recived 
\			SI4432-Receive
\	 	then
\ 4 exti_pr bis!
\ ;

: si4432-start
si4432-init
SI4432-Receive
\ ['] si4432-irq irq-exti2 !
\ 1 14 lshift rcc_apb2enr bis!	\ Enable syscfg registr 
\ %11 8 lshift syscfg_exticr1 bis!	\ select PD.2 ext interrupt
\ 1 2 lshift exti_rtsr bic!
\ 1 2 lshift exti_ftsr bis!
\ EXTI2_IRQn nvic-enable
\ ['] enc-irq irq-exti0 ! 
\ EXTI2_IRQn nvic-enable		\ irq exti2 enable
\ $100 afio_exticr1 bis!
\ 4 exti_ftsr bis!	\ прерывание по 
\ 4 exti_rtsr bic!	\

\ 4 exti_imr bis!		\ разрешаем прерывание 
\ 1 2 lshift exti_imr bis!	\ Enable 2 ext interrupt 
;
