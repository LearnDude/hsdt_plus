In this app, we will take an existing code base from Hearthstone Deck Tracker and modify it to achieve two things to start with.

One is we're building a positioning simulator which will take an existing board and look at the most recent combat and simulate all the possible permutations of placings of minions that the active player could have done, and it will then relate the results and point out which of all the possible permutations was the best one, but also where in this stack the current one that was actually selected by the player was placed.

Secondly, we will also just make sure we have a way of accessing the current screen state so that we can collect more information. Specifically, I want to read the battle history of all the opponents and get that information.