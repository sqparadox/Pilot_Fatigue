# Pilot_Fatigue
Pilot Fatigue Mod for BATTLETECH
Pilot Fatigue:

***SUMMARY***
Now after every mission your pilots that participated are now fatigued. They will have decreased Gunnery, Piloting, and Tactics while fatigued. If they drop while 
fatigued, they have a chance to suffer a "Light Injury." High Guts helps to lower their fatigue and resist light injuries. Morale also helps them to lower their
fatigue. Treat your MechWarriors well and they'll keep fighting until the bitter end!



***CALCULATIONS***
They are out for the following time (default values used): 

	-Time Out = 6 - Guts/2 ± Morale penalty/bonus
	-7.5% of total skill lost per day of fatigue remaining, rounded up
	-1 Resolve lost for every 2.5 days of fatigue remaining, rounded up.
	-Chance to resist a light injury = guts * 10%
	-InjuriesHurt `true` and InjuryReductionIsPercent `false`: 1 Injury = 1 point of skill deduction
	-InjuriesHurt `true` and InjuryReductionIsPercent `true` : (Injuries / Health) x skill level
	-InjuriesHurt `true` and CanPilotInjured `true` : MedTech points >= 3 times the pilot's number of injuries

***ADJUSTABLE VALUES***
Values that can be changed in the settings.json:

	"ArgoUpgradeReduction" : true,		(Do The Hydroponics, Pool, and Arcade Argo Upgrades reduce fatigue time?)
	"FatigueTimeStart" : 6, 			(How many days to start the fatigue calculation at?)
	"FatigueMinimum" : 1, 				(minimum days that a pilot will be fatigued for after a mission)
	"MoralePositiveTierOne" : 5, 		(Positive difference from StartingMorale to reduce fatigue by 1 day)
	"MoralePostiveTierTwo" : 15, 		(Positive difference from StartingMorale to reduce fatigue by 2 day)
	"MoraleNegativeTierOne" : -5, 		(Negative difference from StartingMorale to increase fatigue by 1 day)
	"MoraleNegativeTierTwo" : -15, 		(Negative difference from StartingMorale to increase fatigue by 2 day)
	"UseCumulativeDays" : true,			(When `true` penalites are calculated based on days remaining instead of initial days out)
	"FatigueReducesSkills" : false,		(When `true` fatigue reduces a pilot's skills)
	"FatigueFactor" : 7.5,				(When calculating skill degradation from fatigue, one skill point is worth how many days of fatigue?)
	"FatigueFactorIsPercent" : true,	(When `true` FatigueFactor is a percantage of total skill lost per day) 
	"FatigueReducesResolve" : true,		﻿(When `true` dropping a fatigued pilot reduces resolve)
	"FatigueResolveFactor" : 2.5,		(When calculating reduced resolve, one resolve point is worth how many days of fatigue?)
	"AllowNegativeResolve" : false,		(Can resolve per turn go below zero?)
	"FatigueCausesLowSpirits" : true,	(Does dropping a fatigued pilot cause Low Spirits?)
	"LowMoraleTime" : 14,				(How many days does Low Spirits last?)
	"LightInjuriesOn" : true,			﻿(Can dropping a fatigued pilot cause Light Injuries?)
	"InjuriesHurt" : false, 			(When `true` your skills degrade one point per pilot injury)
	"InjuryReductionIsPercent" : true,	(When `true` skill reduction for injured pilots is calculated based on percantage of health lost)
	"CanPilotInjured" : false,			(When `true` pilots can drop while injured. Also applies to light injures. Requires InjuriesHurt to also be `true`)
	"MaximumFatigueTime" : 14,			(Maximum days of fatigue a pilot can potentially receive)
	"MechDamageMaxDays" : 5,			(Maximum additional days of fatigue a pilot can receive due to mech damage)
	
	"QuirksEnabled" : true,				(When `true` Pilot Quirks can effect fatigue. The below settings require this to be `true`)
	"pilot_wealthy_extra_fatigue" : 2,					(Extra days of fatigue received for pilots with the tag pilot_wealthy)
	"pilot_athletic_FatigueDaysReductionFactor" : 50.0, (Percentage of total potential fatigue removed from pilots with the tag pilot_athletic
	"pilot_athletic_FatigueDaysReduction" : 1,			(Bonus days of fatigue reduction for pilots with the tag pilot_athletic)



***INSTALLATION NOTES***
1) This Mod Conflicts with No Time To Bleed and InjuriesHurt. It will not run if you have these installed and enabled! 

2) Unzip the folder and add it to your BATTLETECH\mods folder. 

3) Make sure you hae BMTL and ModTek installed.
