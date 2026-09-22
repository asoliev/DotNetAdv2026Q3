_Please, complete the following task:_ 

You need to implement role-based security for your Catalog service endpoints using JWT tokens.

**Task 1.**

Setup identity management system. Identity Management System should have the following functionality: 

1. Predefined roles: Manager, Store customer 
2. Predefined permissions: 
- Store customer: Read 
- Manager: Read, Create, Update, Delete 
    

3. Generate Identity token 

4. Check/verify Identity token 

5. Implement Refresh token 
    > Note! You may use some cloud-based IMS (Identity Management Services) or look for existing free solutions like: 
   *  [https://github.com/DuendeSoftware/IdentityServer](https://github.com/DuendeSoftware/IdentityServer) 
   *  [https://github.com/IdentityServer/IdentityServer4](https://github.com/IdentityServer/IdentityServer4) 
   *  [https://www.keycloak.org/](https://www.keycloak.org/) 
    
**Task 2.**  

* Catalog service - secure create/update/delete endpoints to be accessible for Manager role only. All Read (get) endpoints should not have any access limitations for the Manager role. 

* Cart service – all endpoints should be accessible for both roles but add a custom middleware needs to be added to log an identity access token detail. 

Both services must be accessible via the same tokens. 

> **NB!** Scoreboard:

* 1-59 – The written answers to the ‘Self-check questions’ do not have significant issues.   
* 60-89 – First task has been completed. 
* 90-100 – Both tasks have been completed. 